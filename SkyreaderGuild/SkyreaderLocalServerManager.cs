using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BepInEx;
using Newtonsoft.Json;

namespace SkyreaderGuild
{
    internal sealed class SkyreaderLocalServerManager
    {
        private const int MinimumPythonMajor = 3;
        private const int MinimumPythonMinor = 12;
        private const int HealthPollIntervalMs = 1000;
        private const int HealthStartupTimeoutMs = 45000;
        private const int CommandTimeoutMs = 300000;

        private static readonly HttpClient HealthHttp = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };

        private readonly Func<bool> onlineEnabledProvider;
        private readonly Func<string> serverUrlProvider;
        private readonly string packageRoot;
        private readonly object sync = new object();
        private readonly object logSync = new object();
        private readonly string stateRoot;
        private readonly string runtimeRoot;
        private readonly string logRoot;
        private readonly string bootstrapLogPath;
        private readonly string jwtSecretPath;
        private readonly string installStatePath;
        private readonly string databasePath;

        private BootstrapState state;
        private string statusMessage;
        private System.Threading.Tasks.Task bootstrapTask;
        private Process ownedProcess;
        private bool shuttingDown;

        public SkyreaderLocalServerManager(Func<bool> onlineEnabledProvider, Func<string> serverUrlProvider, string packageRoot)
        {
            this.onlineEnabledProvider = onlineEnabledProvider;
            this.serverUrlProvider = serverUrlProvider;
            this.packageRoot = packageRoot ?? string.Empty;

            stateRoot = Path.Combine(Paths.ConfigPath, "SkyreaderGuild", "LocalServer");
            runtimeRoot = Path.Combine(stateRoot, "runtime");
            logRoot = Path.Combine(stateRoot, "logs");
            bootstrapLogPath = Path.Combine(logRoot, "bootstrap.log");
            jwtSecretPath = Path.Combine(stateRoot, "skyreader_jwt_secret.txt");
            installStatePath = Path.Combine(runtimeRoot, "install_state.json");
            databasePath = Path.Combine(stateRoot, "state", "skyreader.db");

            Directory.CreateDirectory(stateRoot);
            Directory.CreateDirectory(runtimeRoot);
            Directory.CreateDirectory(logRoot);
            Directory.CreateDirectory(Path.GetDirectoryName(databasePath) ?? stateRoot);
        }

        public void InitializeOnPluginLoad()
        {
            EnsureBootstrapStarted();
        }

        public void EnsureBootstrapStarted()
        {
            BootstrapDecision decision = EvaluateBootstrapDecision();
            if (!decision.ShouldManage)
            {
                if (!string.IsNullOrEmpty(decision.BlockingReason))
                {
                    SetFailed(decision.BlockingReason);
                }
                return;
            }

            lock (sync)
            {
                if (state == BootstrapState.Ready || state == BootstrapState.Failed || bootstrapTask != null)
                {
                    return;
                }

                state = BootstrapState.Pending;
                statusMessage = "The local Skyreader server is starting in the background. Check again in a moment.";
                bootstrapTask = System.Threading.Tasks.Task.Run(() => BootstrapAsync(decision.Uri));
            }
        }

        public void EnsureRequestReady()
        {
            BootstrapDecision decision = EvaluateBootstrapDecision();
            if (!decision.ShouldManage)
            {
                if (!string.IsNullOrEmpty(decision.BlockingReason))
                {
                    throw new InvalidOperationException(decision.BlockingReason);
                }
                return;
            }

            EnsureBootstrapStarted();

            lock (sync)
            {
                if (state == BootstrapState.Pending)
                {
                    throw new InvalidOperationException(statusMessage ?? "The local Skyreader server is still starting.");
                }

                if (state == BootstrapState.Failed)
                {
                    throw new InvalidOperationException(statusMessage ?? "The local Skyreader server failed to start.");
                }
            }
        }

        public string GetStatusMessage()
        {
            BootstrapDecision decision = EvaluateBootstrapDecision();
            if (!decision.ShouldManage)
            {
                return decision.BlockingReason;
            }

            lock (sync)
            {
                if (state == BootstrapState.Pending || state == BootstrapState.Failed)
                {
                    return statusMessage;
                }
            }

            return null;
        }

        public void Shutdown()
        {
            Process processToStop = null;
            lock (sync)
            {
                shuttingDown = true;
                processToStop = ownedProcess;
                ownedProcess = null;
            }

            if (processToStop == null)
            {
                return;
            }

            try
            {
                if (!processToStop.HasExited)
                {
                    AppendLog("Stopping owned local Skyreader server process.");
                    processToStop.Kill();
                    processToStop.WaitForExit(5000);
                }
            }
            catch (Exception ex)
            {
                SkyreaderGuild.Log("Failed to stop local Skyreader server: " + ex.Message);
            }
            finally
            {
                processToStop.Dispose();
            }
        }

        private async System.Threading.Tasks.Task BootstrapAsync(Uri serverUri)
        {
            try
            {
                AppendLog($"Bootstrap requested for {serverUri}.");

                if (await ProbeHealthAsync(serverUri).ConfigureAwait(false))
                {
                    AppendLog("Reusing existing healthy local Skyreader server.");
                    SetReady();
                    return;
                }

                string sourceRoot = ResolveSourceRoot();
                PythonCandidate python = await DetectPythonAsync().ConfigureAwait(false);
                AppendLog($"Using system Python {python.VersionText} at {python.ExecutablePath}.");

                string venvPython = Path.Combine(runtimeRoot, "venv", "Scripts", "python.exe");
                if (!File.Exists(venvPython))
                {
                    await RunCommandAsync(
                        python.ExecutablePath,
                        "-m venv " + QuoteArg(Path.Combine(runtimeRoot, "venv")),
                        stateRoot,
                        null
                    ).ConfigureAwait(false);
                }

                if (NeedsPackageInstall(sourceRoot))
                {
                    await RunCommandAsync(
                        venvPython,
                        "-m pip install --disable-pip-version-check --upgrade " + QuoteArg(sourceRoot),
                        stateRoot,
                        new Dictionary<string, string> { { "PIP_DISABLE_PIP_VERSION_CHECK", "1" } }
                    ).ConfigureAwait(false);
                    SaveInstallState(new InstallState
                    {
                        SourceStamp = ComputeSourceStamp(sourceRoot),
                        PythonVersion = python.VersionText,
                    });
                }

                StartOwnedServerProcess(serverUri, venvPython);

                if (!await WaitForHealthyAsync(serverUri).ConfigureAwait(false))
                {
                    throw new InvalidOperationException("The local Skyreader server did not become healthy within 45 seconds. See the bootstrap log for details.");
                }

                AppendLog("Local Skyreader server is healthy.");
                SetReady();
            }
            catch (Exception ex)
            {
                SetFailed("The local Skyreader server failed to start: " + ex.Message);
            }
            finally
            {
                lock (sync)
                {
                    bootstrapTask = null;
                }
            }
        }

        private BootstrapDecision EvaluateBootstrapDecision()
        {
            if (!onlineEnabledProvider())
            {
                return BootstrapDecision.NotManaged();
            }

            string rawUrl = serverUrlProvider();
            if (string.IsNullOrWhiteSpace(rawUrl))
            {
                return BootstrapDecision.Blocked("Skyreader ladder server URL is empty.");
            }

            Uri uri;
            if (!Uri.TryCreate(rawUrl.Trim(), UriKind.Absolute, out uri))
            {
                return BootstrapDecision.Blocked("Skyreader ladder server URL is invalid. Use a full URL such as http://localhost:8000.");
            }

            if (!IsLoopbackUri(uri))
            {
                return BootstrapDecision.NotManaged();
            }

            if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
            {
                return BootstrapDecision.Blocked("Local Skyreader bootstrap only supports http:// loopback URLs.");
            }

            return BootstrapDecision.Managed(uri);
        }

        private static bool IsLoopbackUri(Uri uri)
        {
            if (uri == null)
            {
                return false;
            }

            if (string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            IPAddress address;
            return IPAddress.TryParse(uri.Host, out address) && IPAddress.IsLoopback(address);
        }

        private string ResolveSourceRoot()
        {
            string sourceRoot = Path.Combine(packageRoot, "Server", "SkyreaderGuildServer");
            if (!File.Exists(Path.Combine(sourceRoot, "pyproject.toml")))
            {
                throw new InvalidOperationException("Bundled Skyreader server source was not found next to the mod package.");
            }

            return sourceRoot;
        }

        private async System.Threading.Tasks.Task<PythonCandidate> DetectPythonAsync()
        {
            string snippet = "import sys; print(sys.executable); print('.'.join(map(str, sys.version_info[:3])))";
            string quotedSnippet = QuoteArg(snippet);
            List<PythonProbe> probes = new List<PythonProbe>
            {
                new PythonProbe("py", "-3 -c " + quotedSnippet),
                new PythonProbe("python", "-c " + quotedSnippet),
            };
            List<string> failures = new List<string>();

            foreach (PythonProbe probe in probes)
            {
                try
                {
                    CommandResult result = await RunCommandAsync(probe.FileName, probe.Arguments, stateRoot, null).ConfigureAwait(false);
                    string[] lines = (result.StandardOutput ?? string.Empty)
                        .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    if (lines.Length < 2)
                    {
                        failures.Add($"Probe '{probe.FileName}' returned incomplete version data.");
                        continue;
                    }

                    System.Version version;
                    if (!System.Version.TryParse(lines[1].Trim(), out version))
                    {
                        failures.Add($"Probe '{probe.FileName}' returned an unreadable version: {lines[1].Trim()}.");
                        continue;
                    }

                    if (version.Major < MinimumPythonMajor ||
                        (version.Major == MinimumPythonMajor && version.Minor < MinimumPythonMinor))
                    {
                        failures.Add($"Python {version} is too old. Skyreader requires Python {MinimumPythonMajor}.{MinimumPythonMinor}+.");
                        continue;
                    }

                    return new PythonCandidate
                    {
                        ExecutablePath = lines[0].Trim(),
                        VersionText = lines[1].Trim(),
                    };
                }
                catch (Exception ex)
                {
                    failures.Add($"Probe '{probe.FileName}' failed: {ex.Message}");
                }
            }

            throw new InvalidOperationException(
                "Could not find a supported system Python installation. Install Python 3.12+ and ensure `py` or `python` is available on PATH. " +
                string.Join(" ", failures.ToArray())
            );
        }

        private bool NeedsPackageInstall(string sourceRoot)
        {
            string venvPython = Path.Combine(runtimeRoot, "venv", "Scripts", "python.exe");
            if (!File.Exists(venvPython))
            {
                return true;
            }

            InstallState installState = LoadInstallState();
            if (installState == null)
            {
                return true;
            }

            return !string.Equals(installState.SourceStamp, ComputeSourceStamp(sourceRoot), StringComparison.Ordinal);
        }

        private string ComputeSourceStamp(string sourceRoot)
        {
            long newestTicks = 0;
            foreach (string path in Directory.GetFiles(sourceRoot, "*", SearchOption.AllDirectories))
            {
                if (path.IndexOf("__pycache__", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }

                long ticks = File.GetLastWriteTimeUtc(path).Ticks;
                if (ticks > newestTicks)
                {
                    newestTicks = ticks;
                }
            }

            return newestTicks.ToString();
        }

        private InstallState LoadInstallState()
        {
            if (!File.Exists(installStatePath))
            {
                return null;
            }

            try
            {
                return JsonConvert.DeserializeObject<InstallState>(File.ReadAllText(installStatePath));
            }
            catch (Exception ex)
            {
                AppendLog("Failed to read install state: " + ex.Message);
                return null;
            }
        }

        private void SaveInstallState(InstallState installState)
        {
            File.WriteAllText(installStatePath, JsonConvert.SerializeObject(installState, Formatting.Indented));
        }

        private void StartOwnedServerProcess(Uri serverUri, string venvPython)
        {
            string host = string.IsNullOrWhiteSpace(serverUri.Host) ? "127.0.0.1" : serverUri.Host;
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = venvPython,
                Arguments = "-m skyreaderguild_server --host " + QuoteArg(host) + " --port " + serverUri.Port + " --log-level warning",
                WorkingDirectory = stateRoot,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            startInfo.EnvironmentVariables["SKYREADER_JWT_SECRET"] = LoadOrCreateJwtSecret();
            startInfo.EnvironmentVariables["SKYREADER_DB_PATH"] = databasePath;
            startInfo.EnvironmentVariables["PYTHONUNBUFFERED"] = "1";
            startInfo.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";

            Process process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            process.Exited += (sender, args) =>
            {
                bool ignoreExit;
                lock (sync)
                {
                    ignoreExit = shuttingDown || !ReferenceEquals(ownedProcess, process);
                    if (!ignoreExit)
                    {
                        ownedProcess = null;
                    }
                }

                AppendLog("Local server process exited with code " + process.ExitCode + ".");
                if (!ignoreExit)
                {
                    SetFailed("The local Skyreader server stopped unexpectedly. See the bootstrap log for details.");
                }

                process.Dispose();
            };
            process.OutputDataReceived += (sender, args) =>
            {
                if (args.Data != null)
                {
                    AppendLog("[server stdout] " + args.Data);
                }
            };
            process.ErrorDataReceived += (sender, args) =>
            {
                if (args.Data != null)
                {
                    AppendLog("[server stderr] " + args.Data);
                }
            };

            AppendLog("Starting local server process: " + startInfo.FileName + " " + startInfo.Arguments);
            if (!process.Start())
            {
                throw new InvalidOperationException("The local Skyreader server process could not be started.");
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            lock (sync)
            {
                ownedProcess = process;
            }
        }

        private string LoadOrCreateJwtSecret()
        {
            if (File.Exists(jwtSecretPath))
            {
                return File.ReadAllText(jwtSecretPath).Trim();
            }

            byte[] bytes = new byte[48];
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }

            string secret = Convert.ToBase64String(bytes);
            File.WriteAllText(jwtSecretPath, secret);
            return secret;
        }

        private async System.Threading.Tasks.Task<bool> ProbeHealthAsync(Uri serverUri)
        {
            try
            {
                HttpResponseMessage response = await HealthHttp.GetAsync(BuildHealthUri(serverUri)).ConfigureAwait(false);
                using (response)
                {
                    return response.IsSuccessStatusCode;
                }
            }
            catch
            {
                return false;
            }
        }

        private async System.Threading.Tasks.Task<bool> WaitForHealthyAsync(Uri serverUri)
        {
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(HealthStartupTimeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                if (await ProbeHealthAsync(serverUri).ConfigureAwait(false))
                {
                    return true;
                }

                lock (sync)
                {
                    if (ownedProcess != null && ownedProcess.HasExited)
                    {
                        return false;
                    }
                }

                await System.Threading.Tasks.Task.Delay(HealthPollIntervalMs).ConfigureAwait(false);
            }

            return false;
        }

        private static Uri BuildHealthUri(Uri serverUri)
        {
            UriBuilder builder = new UriBuilder(serverUri)
            {
                Path = "/health",
                Query = string.Empty,
            };
            return builder.Uri;
        }

        private async System.Threading.Tasks.Task<CommandResult> RunCommandAsync(string fileName, string arguments, string workingDirectory, IDictionary<string, string> environment)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            if (environment != null)
            {
                foreach (KeyValuePair<string, string> pair in environment)
                {
                    startInfo.EnvironmentVariables[pair.Key] = pair.Value;
                }
            }

            AppendLog("Running command: " + fileName + " " + arguments);

            using (Process process = new Process { StartInfo = startInfo, EnableRaisingEvents = true })
            {
                try
                {
                    process.Start();
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Could not start `{fileName}`: {ex.Message}", ex);
                }

                System.Threading.Tasks.Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
                System.Threading.Tasks.Task<string> stderrTask = process.StandardError.ReadToEndAsync();
                bool exited = await WaitForExitAsync(process, CommandTimeoutMs).ConfigureAwait(false);
                if (!exited)
                {
                    TryKill(process);
                    throw new InvalidOperationException($"Command timed out after {CommandTimeoutMs / 1000} seconds: {fileName} {arguments}");
                }

                string stdout = await stdoutTask.ConfigureAwait(false);
                string stderr = await stderrTask.ConfigureAwait(false);

                if (!string.IsNullOrWhiteSpace(stdout))
                {
                    AppendLog(stdout.TrimEnd());
                }

                if (!string.IsNullOrWhiteSpace(stderr))
                {
                    AppendLog(stderr.TrimEnd());
                }

                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException($"Command failed with exit code {process.ExitCode}: {fileName} {arguments}");
                }

                return new CommandResult
                {
                    ExitCode = process.ExitCode,
                    StandardOutput = stdout,
                    StandardError = stderr,
                };
            }
        }

        private static void TryKill(Process process)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill();
                }
            }
            catch
            {
            }
        }

        private static System.Threading.Tasks.Task<bool> WaitForExitAsync(Process process, int timeoutMs)
        {
            if (process.HasExited)
            {
                return System.Threading.Tasks.Task.FromResult(true);
            }

            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
            Timer timer = null;
            EventHandler handler = null;
            handler = (sender, args) =>
            {
                process.Exited -= handler;
                if (timer != null)
                {
                    timer.Dispose();
                }
                tcs.TrySetResult(true);
            };

            process.Exited += handler;
            timer = new Timer(_ =>
            {
                process.Exited -= handler;
                timer.Dispose();
                tcs.TrySetResult(false);
            }, null, timeoutMs, Timeout.Infinite);

            if (process.HasExited)
            {
                process.Exited -= handler;
                timer.Dispose();
                tcs.TrySetResult(true);
            }

            return tcs.Task;
        }

        private void SetReady()
        {
            lock (sync)
            {
                state = BootstrapState.Ready;
                statusMessage = null;
            }

            SkyreaderGuild.Log("Local Skyreader server is ready.");
        }

        private void SetFailed(string message)
        {
            Process processToStop = null;
            lock (sync)
            {
                if (state == BootstrapState.Failed && string.Equals(statusMessage, message, StringComparison.Ordinal))
                {
                    return;
                }

                state = BootstrapState.Failed;
                statusMessage = message + " See " + bootstrapLogPath + ".";
                processToStop = ownedProcess;
                ownedProcess = null;
            }

            if (processToStop != null)
            {
                TryKill(processToStop);
                processToStop.Dispose();
            }

            AppendLog(message);
            SkyreaderGuild.Log(message);
        }

        private void AppendLog(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";
            lock (logSync)
            {
                File.AppendAllText(bootstrapLogPath, line, Encoding.UTF8);
            }
        }

        private static string QuoteArg(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
        }

        private enum BootstrapState
        {
            Pending,
            Ready,
            Failed,
        }

        private sealed class BootstrapDecision
        {
            public Uri Uri { get; private set; }
            public bool ShouldManage { get; private set; }
            public string BlockingReason { get; private set; }

            public static BootstrapDecision Managed(Uri uri)
            {
                return new BootstrapDecision { Uri = uri, ShouldManage = true };
            }

            public static BootstrapDecision NotManaged()
            {
                return new BootstrapDecision();
            }

            public static BootstrapDecision Blocked(string message)
            {
                return new BootstrapDecision { BlockingReason = message };
            }
        }

        private sealed class PythonProbe
        {
            public PythonProbe(string fileName, string arguments)
            {
                FileName = fileName;
                Arguments = arguments;
            }

            public string FileName { get; private set; }
            public string Arguments { get; private set; }
        }

        private sealed class PythonCandidate
        {
            public string ExecutablePath { get; set; }
            public string VersionText { get; set; }
        }

        private sealed class InstallState
        {
            [JsonProperty("source_stamp")]
            public string SourceStamp { get; set; }

            [JsonProperty("python_version")]
            public string PythonVersion { get; set; }
        }

        private sealed class CommandResult
        {
            public int ExitCode { get; set; }
            public string StandardOutput { get; set; }
            public string StandardError { get; set; }
        }
    }
}

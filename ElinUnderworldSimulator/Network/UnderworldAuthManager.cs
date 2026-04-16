using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using BepInEx;

using Newtonsoft.Json;

namespace ElinUnderworldSimulator
{
    internal sealed class UnderworldAuthManager
    {
        private readonly Func<string> serverUrlProvider;
        private readonly UnderworldLocalServerManager localServerManager;
        private readonly object sync = new object();
        private readonly string identityPath;
        private IdentityData identity;

        public UnderworldAuthManager(Func<string> serverUrlProvider, UnderworldLocalServerManager localServerManager)
        {
            this.serverUrlProvider = serverUrlProvider;
            this.localServerManager = localServerManager;
            string dir = Path.Combine(Paths.ConfigPath, "ElinUnderworldSimulator");
            Directory.CreateDirectory(dir);
            identityPath = Path.Combine(dir, "underworld_identity.json");
            identity = LoadIdentity();
        }

        public async Task<string> GetTokenAsync(HttpClient http, string displayName)
        {
            localServerManager?.EnsureRequestReady();
            IdentityData snapshot;
            lock (sync)
            {
                if (identity == null)
                {
                    identity = new IdentityData { InstallKey = Guid.NewGuid().ToString("N") };
                    SaveIdentity(identity);
                }

                if (!string.IsNullOrEmpty(identity.AuthToken))
                {
                    return identity.AuthToken;
                }

                snapshot = identity.Clone();
            }

            UnderworldRegisterResponse response = await RegisterAsync(http, snapshot.InstallKey, displayName).ConfigureAwait(false);
            lock (sync)
            {
                identity.InstallKey = snapshot.InstallKey;
                identity.AuthToken = response.AuthToken;
                identity.PlayerId = response.PlayerId;
                SaveIdentity(identity);
                return identity.AuthToken;
            }
        }

        public async Task<string> RefreshOrRegisterTokenAsync(HttpClient http, string displayName)
        {
            localServerManager?.EnsureRequestReady();
            IdentityData snapshot;
            lock (sync)
            {
                if (identity == null || string.IsNullOrEmpty(identity.InstallKey))
                {
                    identity = new IdentityData { InstallKey = Guid.NewGuid().ToString("N") };
                    SaveIdentity(identity);
                }

                snapshot = identity.Clone();
            }

            UnderworldRegisterResponse response = await RegisterAsync(http, snapshot.InstallKey, displayName).ConfigureAwait(false);
            lock (sync)
            {
                identity.InstallKey = snapshot.InstallKey;
                identity.AuthToken = response.AuthToken;
                identity.PlayerId = response.PlayerId;
                SaveIdentity(identity);
                return identity.AuthToken;
            }
        }

        public int? GetPlayerId()
        {
            lock (sync)
            {
                return identity?.PlayerId;
            }
        }

        private async Task<UnderworldRegisterResponse> RegisterAsync(HttpClient http, string installKey, string displayName)
        {
            string url = BuildUrl("/api/register");
            var request = new UnderworldRegisterRequest
            {
                InstallKey = installKey,
                DisplayName = displayName,
                GameVersion = Core.Instance == null ? null : Core.Instance.version.GetText(),
                ModVersion = ModInfo.Version,
            };
            string json = JsonConvert.SerializeObject(request);
            using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
            using (HttpResponseMessage response = await http.PostAsync(url, content).ConfigureAwait(false))
            {
                if (!response.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException($"Underworld registration failed: {(int)response.StatusCode} {response.ReasonPhrase}");
                }

                string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return JsonConvert.DeserializeObject<UnderworldRegisterResponse>(body);
            }
        }

        private string BuildUrl(string path)
        {
            string root = serverUrlProvider().Trim().TrimEnd('/');
            if (string.IsNullOrEmpty(root))
            {
                throw new InvalidOperationException("Underworld server URL is empty.");
            }

            return root + path;
        }

        private IdentityData LoadIdentity()
        {
            if (!File.Exists(identityPath))
            {
                return null;
            }

            try
            {
                return JsonConvert.DeserializeObject<IdentityData>(File.ReadAllText(identityPath));
            }
            catch (Exception ex)
            {
                UnderworldPlugin.Warn("Failed to load Underworld identity: " + ex.Message);
                return null;
            }
        }

        private void SaveIdentity(IdentityData data)
        {
            File.WriteAllText(identityPath, JsonConvert.SerializeObject(data, Formatting.Indented));
        }

        private sealed class IdentityData
        {
            [JsonProperty("install_key")]
            public string InstallKey { get; set; }

            [JsonProperty("auth_token")]
            public string AuthToken { get; set; }

            [JsonProperty("player_id")]
            public int? PlayerId { get; set; }

            public IdentityData Clone()
            {
                return new IdentityData
                {
                    InstallKey = InstallKey,
                    AuthToken = AuthToken,
                    PlayerId = PlayerId,
                };
            }
        }
    }
}

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using BepInEx;
using Newtonsoft.Json;

namespace SkyreaderGuild
{
    internal sealed class SkyreaderAuthManager
    {
        private readonly Func<string> serverUrlProvider;
        private readonly object sync = new object();
        private readonly string identityPath;
        private IdentityData identity;

        public SkyreaderAuthManager(Func<string> serverUrlProvider)
        {
            this.serverUrlProvider = serverUrlProvider;
            string dir = Path.Combine(Paths.ConfigPath, "SkyreaderGuild");
            Directory.CreateDirectory(dir);
            identityPath = Path.Combine(dir, "skyreader_identity.json");
            identity = LoadIdentity();
        }

        public async System.Threading.Tasks.Task<string> GetTokenAsync(HttpClient http, string displayName)
        {
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

            AuthResponse response = await RegisterAsync(http, snapshot.InstallKey, displayName).ConfigureAwait(false);
            lock (sync)
            {
                identity.InstallKey = snapshot.InstallKey;
                identity.AuthToken = response.AuthToken;
                identity.AccountId = response.AccountId;
                SaveIdentity(identity);
                return identity.AuthToken;
            }
        }

        public async System.Threading.Tasks.Task<string> RefreshOrRegisterTokenAsync(HttpClient http, string displayName)
        {
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

            string url = BuildUrl("/guild/refresh-token");
            string json = JsonConvert.SerializeObject(new RefreshRequest { InstallKey = snapshot.InstallKey });
            using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
            using (HttpResponseMessage response = await http.PostAsync(url, content).ConfigureAwait(false))
            {
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    AuthResponse registered = await RegisterAsync(http, snapshot.InstallKey, displayName).ConfigureAwait(false);
                    return SaveAuthResponse(snapshot.InstallKey, registered);
                }

                if (!response.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException($"Skyreader token refresh failed: {(int)response.StatusCode} {response.ReasonPhrase}");
                }

                string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                AuthResponse auth = JsonConvert.DeserializeObject<AuthResponse>(body);
                return SaveAuthResponse(snapshot.InstallKey, auth);
            }
        }

        private async System.Threading.Tasks.Task<AuthResponse> RegisterAsync(HttpClient http, string installKey, string displayName)
        {
            string url = BuildUrl("/guild/register-anon");
            var request = new RegisterRequest
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
                    throw new InvalidOperationException($"Skyreader registration failed: {(int)response.StatusCode} {response.ReasonPhrase}");
                }

                string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return JsonConvert.DeserializeObject<AuthResponse>(body);
            }
        }

        private string SaveAuthResponse(string installKey, AuthResponse auth)
        {
            lock (sync)
            {
                identity.InstallKey = installKey;
                identity.AuthToken = auth.AuthToken;
                identity.AccountId = auth.AccountId;
                SaveIdentity(identity);
                return identity.AuthToken;
            }
        }

        private string BuildUrl(string path)
        {
            string root = serverUrlProvider().Trim().TrimEnd('/');
            if (string.IsNullOrEmpty(root))
            {
                throw new InvalidOperationException("Skyreader ladder server URL is empty.");
            }
            return root + path;
        }

        private IdentityData LoadIdentity()
        {
            if (!File.Exists(identityPath)) return null;
            try
            {
                return JsonConvert.DeserializeObject<IdentityData>(File.ReadAllText(identityPath));
            }
            catch (Exception ex)
            {
                SkyreaderGuild.Log("Failed to load Skyreader ladder identity: " + ex.Message);
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

            [JsonProperty("account_id")]
            public string AccountId { get; set; }

            public IdentityData Clone()
            {
                return new IdentityData
                {
                    InstallKey = InstallKey,
                    AuthToken = AuthToken,
                    AccountId = AccountId,
                };
            }
        }

        private sealed class RegisterRequest
        {
            [JsonProperty("install_key")]
            public string InstallKey { get; set; }

            [JsonProperty("display_name")]
            public string DisplayName { get; set; }

            [JsonProperty("game_version")]
            public string GameVersion { get; set; }

            [JsonProperty("mod_version")]
            public string ModVersion { get; set; }
        }

        private sealed class RefreshRequest
        {
            [JsonProperty("install_key")]
            public string InstallKey { get; set; }
        }

        private sealed class AuthResponse
        {
            [JsonProperty("auth_token")]
            public string AuthToken { get; set; }

            [JsonProperty("account_id")]
            public string AccountId { get; set; }
        }
    }
}

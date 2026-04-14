using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace SkyreaderGuild
{
    internal sealed class SkyreaderLadderClient
    {
        private const int MaxBatchSize = 100;
        private const int CacheLifetimeRawMinutes = 1440;

        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

        private readonly Func<string> serverUrlProvider;
        private readonly SkyreaderAuthManager authManager;
        private readonly object queueSync = new object();
        private readonly object cacheSync = new object();
        private readonly List<PendingContribution> pending = new List<PendingContribution>();
        private bool flushInProgress;
        private bool fetchInProgress;
        private string lastFetchError;
        private LadderCache cache;

        public SkyreaderLadderClient(Func<string> serverUrlProvider, SkyreaderAuthManager authManager)
        {
            this.serverUrlProvider = serverUrlProvider;
            this.authManager = authManager;
        }

        public void EnqueueContribution(string type, int amount)
        {
            if (amount <= 0 || amount > 10000)
            {
                SkyreaderGuild.Log($"Ignored invalid ladder contribution: type={type}, amount={amount}.");
                return;
            }

            lock (queueSync)
            {
                pending.Add(new PendingContribution
                {
                    Type = type,
                    Amount = amount,
                    LocalEventId = Guid.NewGuid().ToString("N"),
                    Timestamp = DateTimeOffset.UtcNow.ToString("o"),
                });
            }
        }

        public void FlushContributions(string reason)
        {
            if (!SkyreaderGuild.IsOnlineLadderReady()) return;
            string displayName = SkyreaderGuild.GetPlayerDisplayName();
            int highestRank = SkyreaderGuild.GetCurrentRankValue();
            _ = FlushContributionsAsync(displayName, highestRank, reason);
        }

        public void RefreshLadderIfStale()
        {
            RefreshLadder(force: false);
        }

        public void RefreshLadder(bool force, Action onMainThreadUpdated = null)
        {
            if (!SkyreaderGuild.IsOnlineLadderReady()) return;
            int now = EClass.world?.date?.GetRaw() ?? 0;
            lock (cacheSync)
            {
                if (fetchInProgress) return;
                if (!force && cache != null && now - cache.FetchedAtRaw < CacheLifetimeRawMinutes) return;
                fetchInProgress = true;
                lastFetchError = null;
            }

            string displayName = SkyreaderGuild.GetPlayerDisplayName();
            _ = RefreshLadderAsync(displayName, now, onMainThreadUpdated);
        }

        public LadderPlaqueView GetPlaqueView()
        {
            LadderCache snapshot;
            bool fetching;
            string error;
            lock (cacheSync)
            {
                snapshot = cache;
                fetching = fetchInProgress;
                error = lastFetchError;
            }

            var view = new LadderPlaqueView
            {
                IsOnlineReady = SkyreaderGuild.IsOnlineLadderReady(),
                IsFetching = fetching,
                Error = error,
                Entries = new List<LadderPlaqueEntry>(),
            };

            if (snapshot == null)
            {
                return view;
            }

            view.HasCache = true;
            if (snapshot.Self != null)
            {
                view.CurrentDisplayName = snapshot.Self.DisplayName;
                view.CurrentRank = snapshot.Self.Rank;
                view.CurrentScore = snapshot.Self.TotalScore;
                view.Percentile = snapshot.Self.Percentile;
                view.TotalPlayers = snapshot.Self.TotalPlayers;
            }

            if (snapshot.Global?.Entries != null)
            {
                foreach (LadderEntry entry in snapshot.Global.Entries.Take(20))
                {
                    view.Entries.Add(new LadderPlaqueEntry
                    {
                        Rank = entry.Rank,
                        DisplayName = entry.DisplayName,
                        TotalScore = entry.TotalScore,
                        IsPlayer = snapshot.Self?.Rank != null && entry.Rank == snapshot.Self.Rank.Value,
                    });
                }
            }

            return view;
        }

        public string FormatCachedLadderText()
        {
            if (!SkyreaderGuild.IsOnlineLadderReady())
            {
                return "The stars are quiet today.";
            }

            LadderCache snapshot;
            bool fetching;
            lock (cacheSync)
            {
                snapshot = cache;
                fetching = fetchInProgress;
            }

            if (snapshot == null)
            {
                return fetching
                    ? "The Starlight Ladder is aligning. Check the plaque again in a moment."
                    : "The stars are quiet today.";
            }

            var sb = new StringBuilder();
            sb.AppendLine("Starlight Ladder".TagColor(FontColor.Topic));
            if (snapshot.Self != null && snapshot.Self.Rank.HasValue)
            {
                sb.AppendLine($"Your Position: Rank #{snapshot.Self.Rank.Value} - {snapshot.Self.TotalScore} Starlight ({snapshot.Self.Percentile:0.##}th percentile)");
            }
            else
            {
                sb.AppendLine("Your Position: Unranked");
            }

            sb.AppendLine();
            sb.AppendLine("Top Skyreaders".TagColor(FontColor.Topic));
            if (snapshot.Global == null || snapshot.Global.Entries == null || snapshot.Global.Entries.Count == 0)
            {
                sb.AppendLine("No entries have reached the plaque yet.");
            }
            else
            {
                foreach (LadderEntry entry in snapshot.Global.Entries.Take(20))
                {
                    sb.AppendLine($"#{entry.Rank} {entry.DisplayName} - {entry.TotalScore}");
                }
            }

            if (fetching)
            {
                sb.AppendLine();
                sb.AppendLine("The plaque is refreshing its star chart.");
            }

            return sb.ToString();
        }

        private async System.Threading.Tasks.Task FlushContributionsAsync(string displayName, int highestRank, string reason)
        {
            List<PendingContribution> batch;
            lock (queueSync)
            {
                if (flushInProgress || pending.Count == 0) return;
                flushInProgress = true;
                batch = pending.Take(MaxBatchSize).ToList();
            }

            try
            {
                var payload = new ContributionBatch
                {
                    DisplayName = displayName,
                    HighestRank = highestRank,
                    Contributions = batch,
                };

                HttpResponseMessage response = await SendWithAuthRetryAsync(
                    displayName,
                    token => CreateJsonRequest(HttpMethod.Post, "/contributions/batch", payload, token)
                ).ConfigureAwait(false);

                using (response)
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        SkyreaderGuild.Log($"Ladder contribution flush failed ({reason}): {(int)response.StatusCode} {response.ReasonPhrase}");
                        return;
                    }
                }

                var sentIds = new HashSet<string>(batch.Select(c => c.LocalEventId));
                lock (queueSync)
                {
                    pending.RemoveAll(c => sentIds.Contains(c.LocalEventId));
                }
                SkyreaderGuild.Log($"Flushed {batch.Count} Starlight Ladder contribution(s): {reason}.");
            }
            catch (Exception ex)
            {
                SkyreaderGuild.Log("Ladder contribution flush failed: " + ex.Message);
            }
            finally
            {
                lock (queueSync)
                {
                    flushInProgress = false;
                }
            }
        }

        private async System.Threading.Tasks.Task RefreshLadderAsync(string displayName, int fetchedAtRaw, Action onMainThreadUpdated)
        {
            try
            {
                HttpResponseMessage globalResponse = await SendWithAuthRetryAsync(
                    displayName,
                    token => CreateRequest(HttpMethod.Get, "/ladder/global?limit=20", token)
                ).ConfigureAwait(false);
                using (globalResponse)
                {
                    if (!globalResponse.IsSuccessStatusCode)
                    {
                        SetFetchError($"The Starlight Ladder could not be reached ({(int)globalResponse.StatusCode} {globalResponse.ReasonPhrase}).");
                        SkyreaderGuild.Log($"Ladder global fetch failed: {(int)globalResponse.StatusCode} {globalResponse.ReasonPhrase}");
                        return;
                    }

                    HttpResponseMessage selfResponse = await SendWithAuthRetryAsync(
                        displayName,
                        token => CreateRequest(HttpMethod.Get, "/ladder/self", token)
                    ).ConfigureAwait(false);
                    using (selfResponse)
                    {
                        if (!selfResponse.IsSuccessStatusCode)
                        {
                            SetFetchError($"Your Starlight Ladder standing could not be reached ({(int)selfResponse.StatusCode} {selfResponse.ReasonPhrase}).");
                            SkyreaderGuild.Log($"Ladder self fetch failed: {(int)selfResponse.StatusCode} {selfResponse.ReasonPhrase}");
                            return;
                        }

                        string globalBody = await globalResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                        string selfBody = await selfResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                        var next = new LadderCache
                        {
                            FetchedAtRaw = fetchedAtRaw,
                            Global = JsonConvert.DeserializeObject<GlobalLadderResponse>(globalBody),
                            Self = JsonConvert.DeserializeObject<SelfRankResponse>(selfBody),
                        };

                        lock (cacheSync)
                        {
                            cache = next;
                            lastFetchError = null;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                SetFetchError("The Starlight Ladder is unreachable. Check that the ladder server is running.");
                SkyreaderGuild.Log("Ladder fetch failed: " + ex.Message);
            }
            finally
            {
                lock (cacheSync)
                {
                    fetchInProgress = false;
                }
                NotifyRefreshComplete(onMainThreadUpdated);
            }
        }

        private void SetFetchError(string message)
        {
            lock (cacheSync)
            {
                lastFetchError = message;
            }
        }

        private void NotifyRefreshComplete(Action onMainThreadUpdated)
        {
            if (onMainThreadUpdated == null) return;
            try
            {
                if (EClass.core != null)
                {
                    EClass.core.actionsNextFrame.Add(onMainThreadUpdated);
                }
            }
            catch (Exception ex)
            {
                SkyreaderGuild.Log("Could not schedule ladder dialog refresh: " + ex.Message);
            }
        }

        private async System.Threading.Tasks.Task<HttpResponseMessage> SendWithAuthRetryAsync(string displayName, Func<string, HttpRequestMessage> create)
        {
            string token = await authManager.GetTokenAsync(Http, displayName).ConfigureAwait(false);
            HttpResponseMessage response = await Http.SendAsync(create(token)).ConfigureAwait(false);
            if (response.StatusCode != HttpStatusCode.Unauthorized)
            {
                return response;
            }

            response.Dispose();
            token = await authManager.RefreshOrRegisterTokenAsync(Http, displayName).ConfigureAwait(false);
            return await Http.SendAsync(create(token)).ConfigureAwait(false);
        }

        private HttpRequestMessage CreateJsonRequest(HttpMethod method, string path, object payload, string token)
        {
            HttpRequestMessage request = CreateRequest(method, path, token);
            string json = JsonConvert.SerializeObject(payload);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            return request;
        }

        private HttpRequestMessage CreateRequest(HttpMethod method, string path, string token)
        {
            var request = new HttpRequestMessage(method, BuildUrl(path));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return request;
        }

        private string BuildUrl(string path)
        {
            return serverUrlProvider().Trim().TrimEnd('/') + path;
        }

        private sealed class PendingContribution
        {
            [JsonProperty("type")]
            public string Type { get; set; }

            [JsonProperty("amount")]
            public int Amount { get; set; }

            [JsonProperty("local_event_id")]
            public string LocalEventId { get; set; }

            [JsonProperty("timestamp")]
            public string Timestamp { get; set; }
        }

        private sealed class ContributionBatch
        {
            [JsonProperty("display_name")]
            public string DisplayName { get; set; }

            [JsonProperty("highest_rank")]
            public int HighestRank { get; set; }

            [JsonProperty("contributions")]
            public List<PendingContribution> Contributions { get; set; }
        }

        private sealed class LadderCache
        {
            public int FetchedAtRaw { get; set; }
            public GlobalLadderResponse Global { get; set; }
            public SelfRankResponse Self { get; set; }
        }

        private sealed class GlobalLadderResponse
        {
            [JsonProperty("entries")]
            public List<LadderEntry> Entries { get; set; }
        }

        private sealed class LadderEntry
        {
            [JsonProperty("rank")]
            public int Rank { get; set; }

            [JsonProperty("display_name")]
            public string DisplayName { get; set; }

            [JsonProperty("total_score")]
            public int TotalScore { get; set; }
        }

        private sealed class SelfRankResponse
        {
            [JsonProperty("display_name")]
            public string DisplayName { get; set; }

            [JsonProperty("rank")]
            public int? Rank { get; set; }

            [JsonProperty("total_score")]
            public int TotalScore { get; set; }

            [JsonProperty("percentile")]
            public double? Percentile { get; set; }

            [JsonProperty("total_players")]
            public int TotalPlayers { get; set; }
        }

        internal sealed class LadderPlaqueView
        {
            public bool IsOnlineReady { get; set; }
            public bool HasCache { get; set; }
            public bool IsFetching { get; set; }
            public string Error { get; set; }
            public string CurrentDisplayName { get; set; }
            public int? CurrentRank { get; set; }
            public int CurrentScore { get; set; }
            public double? Percentile { get; set; }
            public int TotalPlayers { get; set; }
            public List<LadderPlaqueEntry> Entries { get; set; }
        }

        internal sealed class LadderPlaqueEntry
        {
            public int Rank { get; set; }
            public string DisplayName { get; set; }
            public int TotalScore { get; set; }
            public bool IsPlayer { get; set; }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace SkyreaderGuild
{
    /// <summary>
    /// Client for the Phase 9 online features: Seasons, Constellations, Geometry, Comet Heatmap, Star Papers.
    /// Follows the same async HTTP + auth-retry pattern as SkyreaderLadderClient.
    /// All cached data uses a single lock for simplicity — these features are read-infrequently.
    /// </summary>
    internal sealed class SkyreaderOnlineClient
    {
        private const int CacheLifetimeRawMinutes = 60;

        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

        private readonly Func<string> serverUrlProvider;
        private readonly SkyreaderAuthManager authManager;
        private readonly object sync = new object();

        private bool fetchingSeasons;
        private bool fetchingConstellations;
        private bool fetchingGeometry;
        private bool fetchingHeatmap;
        private bool fetchingNotes;
        private bool fetchingOwnNotes;

        // ── Cached Data ─────────────────────────────────────────────────
        private SeasonData cachedSeason;
        private int seasonFetchedAtRaw;

        private ConstellationsData cachedConstellations;
        private int constellationsFetchedAtRaw;

        private GeometrySummaryData cachedGeometry;
        private int geometryFetchedAtRaw;

        private HeatmapData cachedHeatmap;
        private int heatmapFetchedAtRaw;

        private List<StarPaperEntry> cachedNotes;
        private int notesFetchedAtRaw;
        private List<StarPaperEntry> cachedOwnNotes;
        private int ownNotesFetchedAtRaw;

        public SkyreaderOnlineClient(Func<string> serverUrlProvider, SkyreaderAuthManager authManager)
        {
            this.serverUrlProvider = serverUrlProvider;
            this.authManager = authManager;
        }

        // ═══════════════════════════════════════════════════════════════
        //  SEASONS
        // ═══════════════════════════════════════════════════════════════

        public SeasonData GetCachedSeason() { lock (sync) return cachedSeason; }

        public void RefreshSeason(bool force = false, Action onDone = null)
        {
            int now = RawNow();
            lock (sync)
            {
                if (fetchingSeasons) return;
                if (!force && cachedSeason != null && now - seasonFetchedAtRaw < CacheLifetimeRawMinutes) return;
                fetchingSeasons = true;
            }
            _ = FetchSeasonAsync(now, onDone);
        }

        private async System.Threading.Tasks.Task FetchSeasonAsync(int now, Action onDone)
        {
            try
            {
                string body = await GetJsonAsync("/sky-season/current").ConfigureAwait(false);
                var data = JsonConvert.DeserializeObject<SeasonData>(body);
                lock (sync) { cachedSeason = data; seasonFetchedAtRaw = now; }
            }
            catch (Exception ex) { SkyreaderGuild.Log("Season fetch failed: " + ex.Message); }
            finally { lock (sync) fetchingSeasons = false; NotifyMainThread(onDone); }
        }

        // ═══════════════════════════════════════════════════════════════
        //  CONSTELLATIONS
        // ═══════════════════════════════════════════════════════════════

        public ConstellationsData GetCachedConstellations() { lock (sync) return cachedConstellations; }

        public void RefreshConstellations(bool force = false, Action onDone = null)
        {
            int now = RawNow();
            lock (sync)
            {
                if (fetchingConstellations) return;
                if (!force && cachedConstellations != null && now - constellationsFetchedAtRaw < CacheLifetimeRawMinutes) return;
                fetchingConstellations = true;
            }
            _ = FetchConstellationsAsync(now, onDone);
        }

        private async System.Threading.Tasks.Task FetchConstellationsAsync(int now, Action onDone)
        {
            try
            {
                string body = await GetJsonAsync("/constellations/current").ConfigureAwait(false);
                var data = JsonConvert.DeserializeObject<ConstellationsData>(body);
                lock (sync) { cachedConstellations = data; constellationsFetchedAtRaw = now; }
            }
            catch (Exception ex) { SkyreaderGuild.Log("Constellation fetch failed: " + ex.Message); }
            finally { lock (sync) fetchingConstellations = false; NotifyMainThread(onDone); }
        }

        public void JoinConstellation(string constellationId, Action onDone = null)
        {
            _ = JoinConstellationAsync(constellationId, onDone);
        }

        private async System.Threading.Tasks.Task JoinConstellationAsync(string constellationId, Action onDone)
        {
            try
            {
                var payload = new { constellation_id = constellationId };
                await PostJsonAsync("/constellations/join", payload).ConfigureAwait(false);
                lock (sync) constellationsFetchedAtRaw = 0; // force refresh
            }
            catch (Exception ex) { SkyreaderGuild.Log("Constellation join failed: " + ex.Message); }
            finally { NotifyMainThread(onDone); }
        }

        // ═══════════════════════════════════════════════════════════════
        //  GEOMETRY
        // ═══════════════════════════════════════════════════════════════

        public GeometrySummaryData GetCachedGeometry() { lock (sync) return cachedGeometry; }

        public void SubmitGeometrySample(int dangerBand, string shapeType, int roomCount)
        {
            _ = SubmitGeometrySampleAsync(dangerBand, shapeType, roomCount);
        }

        private async System.Threading.Tasks.Task SubmitGeometrySampleAsync(int dangerBand, string shapeType, int roomCount)
        {
            try
            {
                var payload = new { danger_band = dangerBand, shape_type = shapeType, room_count = roomCount };
                await PostJsonAsync("/geometry/sample", payload).ConfigureAwait(false);
                SkyreaderGuild.Log($"Geometry sample sent: band={dangerBand}, shape={shapeType}, rooms={roomCount}.");
            }
            catch (Exception ex) { SkyreaderGuild.Log("Geometry sample failed: " + ex.Message); }
        }

        public void RefreshGeometry(bool force = false, Action onDone = null)
        {
            int now = RawNow();
            lock (sync)
            {
                if (fetchingGeometry) return;
                if (!force && cachedGeometry != null && now - geometryFetchedAtRaw < CacheLifetimeRawMinutes) return;
                fetchingGeometry = true;
            }
            _ = FetchGeometryAsync(now, onDone);
        }

        private async System.Threading.Tasks.Task FetchGeometryAsync(int now, Action onDone)
        {
            try
            {
                string body = await GetJsonAsync("/geometry/summary").ConfigureAwait(false);
                var data = JsonConvert.DeserializeObject<GeometrySummaryData>(body);
                lock (sync) { cachedGeometry = data; geometryFetchedAtRaw = now; }
            }
            catch (Exception ex) { SkyreaderGuild.Log("Geometry summary fetch failed: " + ex.Message); }
            finally { lock (sync) fetchingGeometry = false; NotifyMainThread(onDone); }
        }

        // ═══════════════════════════════════════════════════════════════
        //  COMET HEATMAP
        // ═══════════════════════════════════════════════════════════════

        public HeatmapData GetCachedHeatmap() { lock (sync) return cachedHeatmap; }

        public void SubmitCometReport(string siteId, string siteName, int worldX, int worldY, int touched, int cleansed)
        {
            _ = SubmitCometReportAsync(siteId, siteName, worldX, worldY, touched, cleansed);
        }

        private async System.Threading.Tasks.Task SubmitCometReportAsync(string siteId, string siteName, int worldX, int worldY, int touched, int cleansed)
        {
            try
            {
                var payload = new
                {
                    site_id = siteId,
                    site_name = siteName,
                    world_x = worldX,
                    world_y = worldY,
                    touched_count = touched,
                    cleansed_count = cleansed
                };
                await PostJsonAsync("/comet/report", payload).ConfigureAwait(false);
                SkyreaderGuild.Log($"Comet report sent: site={siteId}, pos=({worldX},{worldY}), touched={touched}, cleansed={cleansed}.");
            }
            catch (Exception ex) { SkyreaderGuild.Log($"Comet report failed: site={siteId}, pos=({worldX},{worldY}), touched={touched}, cleansed={cleansed}. {ex.Message}"); }
        }

        public void RefreshHeatmap(bool force = false, Action onDone = null)
        {
            int now = RawNow();
            lock (sync)
            {
                if (fetchingHeatmap) return;
                if (!force && cachedHeatmap != null && now - heatmapFetchedAtRaw < CacheLifetimeRawMinutes) return;
                fetchingHeatmap = true;
            }
            _ = FetchHeatmapAsync(now, onDone);
        }

        private async System.Threading.Tasks.Task FetchHeatmapAsync(int now, Action onDone)
        {
            try
            {
                string body = await GetJsonAsync("/comet/heatmap").ConfigureAwait(false);
                var data = JsonConvert.DeserializeObject<HeatmapData>(body);
                lock (sync) { cachedHeatmap = data; heatmapFetchedAtRaw = now; }
            }
            catch (Exception ex) { SkyreaderGuild.Log("Heatmap fetch failed: " + ex.Message); }
            finally { lock (sync) fetchingHeatmap = false; NotifyMainThread(onDone); }
        }

        // ═══════════════════════════════════════════════════════════════
        //  STAR PAPERS
        // ═══════════════════════════════════════════════════════════════

        public List<StarPaperEntry> GetCachedNotes() { lock (sync) return cachedNotes; }
        public List<StarPaperEntry> GetCachedOwnNotes() { lock (sync) return cachedOwnNotes; }

        public void CreateNote(string title, string body, Action<bool, string> onDone = null)
        {
            _ = CreateNoteAsync(title, body, onDone);
        }

        private async System.Threading.Tasks.Task CreateNoteAsync(string title, string body, Action<bool, string> onDone)
        {
            bool success = false;
            string message = null;
            try
            {
                var payload = new { title, body };
                await PostJsonAsync("/research-notes/create", payload).ConfigureAwait(false);
                lock (sync) ownNotesFetchedAtRaw = 0;
                SkyreaderGuild.Log("Star paper submitted.");
                success = true;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                SkyreaderGuild.Log("Star paper create failed: " + ex.Message);
            }
            finally { NotifyMainThread(onDone, success, message); }
        }

        public void PullNotes(int limit = 5, Action onDone = null)
        {
            int now = RawNow();
            lock (sync)
            {
                if (fetchingNotes) return;
                if (cachedNotes != null && now - notesFetchedAtRaw < CacheLifetimeRawMinutes) return;
                fetchingNotes = true;
            }
            _ = PullNotesAsync(now, limit, onDone);
        }

        public void ForcePullNotes(int limit = 5, Action onDone = null)
        {
            lock (sync)
            {
                if (fetchingNotes) return;
                fetchingNotes = true;
            }
            _ = PullNotesAsync(RawNow(), limit, onDone);
        }

        private async System.Threading.Tasks.Task PullNotesAsync(int now, int limit, Action onDone)
        {
            try
            {
                string body = await GetJsonAsync($"/research-notes/pull?limit={limit}").ConfigureAwait(false);
                var data = JsonConvert.DeserializeObject<NotePullResponse>(body);
                lock (sync) { cachedNotes = data?.Notes ?? new List<StarPaperEntry>(); notesFetchedAtRaw = now; }
            }
            catch (Exception ex) { SkyreaderGuild.Log("Star paper pull failed: " + ex.Message); }
            finally { lock (sync) fetchingNotes = false; NotifyMainThread(onDone); }
        }

        public void PullOwnNotes(int limit = 50, Action onDone = null)
        {
            int now = RawNow();
            lock (sync)
            {
                if (fetchingOwnNotes) return;
                if (cachedOwnNotes != null && now - ownNotesFetchedAtRaw < CacheLifetimeRawMinutes) return;
                fetchingOwnNotes = true;
            }
            _ = PullOwnNotesAsync(now, limit, onDone);
        }

        public void ForcePullOwnNotes(int limit = 50, Action onDone = null)
        {
            lock (sync)
            {
                if (fetchingOwnNotes) return;
                fetchingOwnNotes = true;
            }
            _ = PullOwnNotesAsync(RawNow(), limit, onDone);
        }

        private async System.Threading.Tasks.Task PullOwnNotesAsync(int now, int limit, Action onDone)
        {
            try
            {
                string body = await GetJsonAsync($"/research-notes/mine?limit={limit}").ConfigureAwait(false);
                var data = JsonConvert.DeserializeObject<NotePullResponse>(body);
                lock (sync) { cachedOwnNotes = data?.Notes ?? new List<StarPaperEntry>(); ownNotesFetchedAtRaw = now; }
            }
            catch (Exception ex) { SkyreaderGuild.Log("Own star paper pull failed: " + ex.Message); }
            finally { lock (sync) fetchingOwnNotes = false; NotifyMainThread(onDone); }
        }

        public void RateNote(string noteId, int value, Action onDone = null)
        {
            _ = RateNoteAsync(noteId, value, onDone);
        }

        private async System.Threading.Tasks.Task RateNoteAsync(string noteId, int value, Action onDone)
        {
            try
            {
                var payload = new { note_id = noteId, value };
                await PostJsonAsync("/research-notes/rate", payload).ConfigureAwait(false);
            }
            catch (Exception ex) { SkyreaderGuild.Log("Star paper rate failed: " + ex.Message); }
            finally { NotifyMainThread(onDone); }
        }

        // ═══════════════════════════════════════════════════════════════
        //  HTTP Helpers
        // ═══════════════════════════════════════════════════════════════

        private async System.Threading.Tasks.Task<string> GetJsonAsync(string path)
        {
            string displayName = SkyreaderGuild.GetPlayerDisplayName();
            HttpResponseMessage response = await SendWithAuthRetryAsync(
                displayName,
                token => CreateRequest(HttpMethod.Get, path, token)
            ).ConfigureAwait(false);

            using (response)
            {
                string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                    throw new InvalidOperationException($"{(int)response.StatusCode} {response.ReasonPhrase}: {body}");
                return body;
            }
        }

        private async System.Threading.Tasks.Task<string> PostJsonAsync(string path, object payload)
        {
            string displayName = SkyreaderGuild.GetPlayerDisplayName();
            HttpResponseMessage response = await SendWithAuthRetryAsync(
                displayName,
                token => CreateJsonRequest(HttpMethod.Post, path, payload, token)
            ).ConfigureAwait(false);

            using (response)
            {
                string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                    throw new InvalidOperationException($"{(int)response.StatusCode} {response.ReasonPhrase}: {body}");
                return body;
            }
        }

        private async System.Threading.Tasks.Task<HttpResponseMessage> SendWithAuthRetryAsync(string displayName, Func<string, HttpRequestMessage> create)
        {
            string token = await authManager.GetTokenAsync(Http, displayName).ConfigureAwait(false);
            HttpResponseMessage response = await Http.SendAsync(create(token)).ConfigureAwait(false);
            if (response.StatusCode != HttpStatusCode.Unauthorized)
                return response;

            response.Dispose();
            token = await authManager.RefreshOrRegisterTokenAsync(Http, displayName).ConfigureAwait(false);
            return await Http.SendAsync(create(token)).ConfigureAwait(false);
        }

        private HttpRequestMessage CreateJsonRequest(HttpMethod method, string path, object payload, string token)
        {
            HttpRequestMessage request = CreateRequest(method, path, token);
            request.Content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
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

        private static int RawNow()
        {
            return EClass.world?.date?.GetRaw() ?? 0;
        }

        private static void NotifyMainThread(Action action)
        {
            if (action == null) return;
            try
            {
                if (EClass.core != null)
                    EClass.core.actionsNextFrame.Add(action);
            }
            catch (Exception ex)
            {
                SkyreaderGuild.Log("Main-thread callback failed: " + ex.Message);
            }
        }

        private static void NotifyMainThread(Action<bool, string> action, bool success, string message)
        {
            if (action == null) return;
            try
            {
                if (EClass.core != null)
                    EClass.core.actionsNextFrame.Add(() => action(success, message));
            }
            catch (Exception ex)
            {
                SkyreaderGuild.Log("Main-thread callback failed: " + ex.Message);
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  DTO Classes
        // ═══════════════════════════════════════════════════════════════

        internal sealed class SeasonData
        {
            [JsonProperty("id")] public string Id { get; set; }
            [JsonProperty("name")] public string Name { get; set; }
            [JsonProperty("description")] public string Description { get; set; }
            [JsonProperty("starts_at")] public string StartsAt { get; set; }
            [JsonProperty("ends_at")] public string EndsAt { get; set; }
            [JsonProperty("duration_days")] public int DurationDays { get; set; }
            [JsonProperty("modifiers")] public Dictionary<string, object> Modifiers { get; set; }
        }

        internal sealed class ConstellationsData
        {
            [JsonProperty("season_id")] public string SeasonId { get; set; }
            [JsonProperty("season_name")] public string SeasonName { get; set; }
            [JsonProperty("player_constellation_id")] public string PlayerConstellationId { get; set; }
            [JsonProperty("constellations")] public List<ConstellationEntry> Constellations { get; set; }
        }

        internal sealed class ConstellationEntry
        {
            [JsonProperty("id")] public string Id { get; set; }
            [JsonProperty("name")] public string Name { get; set; }
            [JsonProperty("description")] public string Description { get; set; }
            [JsonProperty("lore_domain")] public string LoreDomain { get; set; }
            [JsonProperty("goals")] public Dictionary<string, int> Goals { get; set; }
            [JsonProperty("progress")] public Dictionary<string, int> Progress { get; set; }
            [JsonProperty("member_count")] public int MemberCount { get; set; }
            [JsonProperty("goals_met")] public bool GoalsMet { get; set; }
        }

        internal sealed class GeometrySummaryData
        {
            [JsonProperty("season_name")] public string SeasonName { get; set; }
            [JsonProperty("total_samples")] public int TotalSamples { get; set; }
            [JsonProperty("bands")] public Dictionary<string, Dictionary<string, double>> Bands { get; set; }
            [JsonProperty("dominant_shape")] public string DominantShape { get; set; }
            [JsonProperty("dominant_flavor")] public string DominantFlavor { get; set; }
            [JsonProperty("geometry_bias")] public string GeometryBias { get; set; }
        }

        internal sealed class HeatmapData
        {
            [JsonProperty("season_name")] public string SeasonName { get; set; }
            [JsonProperty("sites")] public List<SiteHeat> Sites { get; set; }
        }

        internal sealed class SiteHeat
        {
            [JsonProperty("site_id")] public string SiteId { get; set; }
            [JsonProperty("site_name")] public string SiteName { get; set; }
            [JsonProperty("world_x")] public int WorldX { get; set; }
            [JsonProperty("world_y")] public int WorldY { get; set; }
            [JsonProperty("touched")] public int Touched { get; set; }
            [JsonProperty("cleansed")] public int Cleansed { get; set; }
            [JsonProperty("ratio")] public double Ratio { get; set; }
            [JsonProperty("status")] public string Status { get; set; }
        }

        internal sealed class StarPaperEntry
        {
            [JsonProperty("id")] public string Id { get; set; }
            [JsonProperty("title")] public string Title { get; set; }
            [JsonProperty("body")] public string Body { get; set; }
            [JsonProperty("rating")] public int Rating { get; set; }
            [JsonProperty("created_at")] public string CreatedAt { get; set; }
        }

        private sealed class NotePullResponse
        {
            [JsonProperty("notes")] public List<StarPaperEntry> Notes { get; set; }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;

namespace ElinUnderworldSimulator
{
    internal sealed class UnderworldNetworkClient
    {
        private readonly Func<string> serverUrlProvider;
        private readonly UnderworldAuthManager authManager;
        private readonly UnderworldNetworkState state;
        private readonly HttpClient http;

        public UnderworldNetworkClient(Func<string> serverUrlProvider, UnderworldAuthManager authManager, UnderworldNetworkState state)
        {
            this.serverUrlProvider = serverUrlProvider;
            this.authManager = authManager;
            this.state = state;
            http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        }

        public async Task<UnderworldLoginResponse> LoginAsync()
        {
            try
            {
                string token = await authManager.GetTokenAsync(http, UnderworldPlugin.GetPlayerDisplayName()).ConfigureAwait(false);
                if (string.IsNullOrEmpty(token))
                {
                    return null;
                }

                UnderworldLoginResponse response = await SendAsync<UnderworldLoginResponse>("/api/login", HttpMethod.Post, new UnderworldLoginRequest { AuthToken = token }, useBearer: false).ConfigureAwait(false);
                return response;
            }
            catch (Exception ex)
            {
                UnderworldPlugin.Warn(UnderworldPlugin.GetOnlineFailureMessage(ex, "Underworld login failed."));
                return null;
            }
        }

        public async Task<List<UnderworldOrderDto>> GetAvailableOrdersAsync(string territoryId = null, int limit = 20)
        {
            string path = "/api/orders/available?limit=" + limit;
            if (!string.IsNullOrEmpty(territoryId))
            {
                path += "&territory_id=" + Uri.EscapeDataString(territoryId);
            }

            UnderworldOrderListResponse response = await SendAsync<UnderworldOrderListResponse>(path, HttpMethod.Get, null, useBearer: true).ConfigureAwait(false);
            if (response != null)
            {
                state.SetAvailableOrders(response.Orders);
            }
            return response?.Orders;
        }

        public async Task<UnderworldAcceptOrderResponse> AcceptOrderAsync(int orderId)
        {
            UnderworldAcceptOrderResponse response = await SendAsync<UnderworldAcceptOrderResponse>(
                "/api/orders/accept",
                HttpMethod.Post,
                new UnderworldAcceptOrderRequest { OrderId = orderId },
                useBearer: true
            ).ConfigureAwait(false);
            if (response != null)
            {
                state.RemoveAcceptedOrder(orderId);
            }
            return response;
        }

        public async Task<UnderworldShipmentSubmitResponse> SubmitShipmentAsync(UnderworldShipmentSubmitRequest payload)
        {
            UnderworldShipmentSubmitResponse response = await SendAsync<UnderworldShipmentSubmitResponse>(
                "/api/shipments/submit",
                HttpMethod.Post,
                payload,
                useBearer: true
            ).ConfigureAwait(false);
            if (response != null)
            {
                state.AddShipmentResult(new UnderworldShipmentResultDto
                {
                    ShipmentId = response.ShipmentId,
                    OrderId = payload.OrderId,
                    Outcome = response.Outcome,
                    SatisfactionScore = response.SatisfactionScore,
                    FinalPayout = response.FinalPayout,
                    HeatDelta = response.HeatDelta,
                    RepDelta = response.RepDelta,
                    EnforcementEvent = response.EnforcementEvent,
                    TerritoryHeatAfter = response.TerritoryHeatAfter,
                });
            }
            return response;
        }

        public async Task<UnderworldShipmentResultsResponse> GetShipmentResultsAsync()
        {
            UnderworldShipmentResultsResponse response = await SendAsync<UnderworldShipmentResultsResponse>("/api/shipments/results", HttpMethod.Get, null, useBearer: true).ConfigureAwait(false);
            if (response != null)
            {
                state.SetResults(response);
            }
            return response;
        }

        public async Task<List<UnderworldTerritoryDto>> GetTerritoriesAsync()
        {
            UnderworldTerritoriesResponse response = await SendAsync<UnderworldTerritoriesResponse>("/api/territories", HttpMethod.Get, null, useBearer: true).ConfigureAwait(false);
            if (response != null)
            {
                state.SetTerritories(response.Territories);
            }
            return response?.Territories;
        }

        public async Task<UnderworldCreateFactionResponse> CreateFactionAsync(string name)
        {
            return await SendAsync<UnderworldCreateFactionResponse>(
                "/api/factions/create",
                HttpMethod.Post,
                new UnderworldCreateFactionRequest { Name = name },
                useBearer: true
            ).ConfigureAwait(false);
        }

        public async Task<UnderworldJoinFactionResponse> JoinFactionAsync(int factionId)
        {
            return await SendAsync<UnderworldJoinFactionResponse>(
                "/api/factions/join",
                HttpMethod.Post,
                new UnderworldJoinFactionRequest { FactionId = factionId },
                useBearer: true
            ).ConfigureAwait(false);
        }

        public async Task<UnderworldFactionDetailDto> GetFactionAsync(int factionId)
        {
            return await SendAsync<UnderworldFactionDetailDto>("/api/factions/" + factionId, HttpMethod.Get, null, useBearer: true).ConfigureAwait(false);
        }

        public async Task<UnderworldLeaveFactionResponse> LeaveFactionAsync()
        {
            return await SendAsync<UnderworldLeaveFactionResponse>("/api/factions/leave", HttpMethod.Post, null, useBearer: true).ConfigureAwait(false);
        }

        public async Task<UnderworldDisbandFactionResponse> DisbandFactionAsync(int factionId)
        {
            return await SendAsync<UnderworldDisbandFactionResponse>("/api/factions/" + factionId, HttpMethod.Delete, null, useBearer: true).ConfigureAwait(false);
        }

        public async Task<UnderworldPromoteMemberResponse> PromoteMemberAsync(int factionId, int playerId)
        {
            return await SendAsync<UnderworldPromoteMemberResponse>(
                "/api/factions/" + factionId + "/promote",
                HttpMethod.Post,
                new UnderworldPromoteMemberRequest { PlayerId = playerId },
                useBearer: true
            ).ConfigureAwait(false);
        }

        public async Task<UnderworldPlayerStatusDto> GetPlayerStatusAsync()
        {
            UnderworldPlayerStatusDto response = await SendAsync<UnderworldPlayerStatusDto>("/api/player/status", HttpMethod.Get, null, useBearer: true).ConfigureAwait(false);
            if (response != null)
            {
                state.SetPlayerStatus(response);
            }
            return response;
        }

        private async Task<T> SendAsync<T>(string path, HttpMethod method, object body, bool useBearer) where T : class
        {
            try
            {
                return await SendCoreAsync<T>(path, method, body, useBearer, retryOnUnauthorized: true).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                UnderworldPlugin.Warn(UnderworldPlugin.GetOnlineFailureMessage(ex, "Underworld network request failed."));
                return null;
            }
        }

        private async Task<T> SendCoreAsync<T>(string path, HttpMethod method, object body, bool useBearer, bool retryOnUnauthorized) where T : class
        {
            string token = null;
            if (useBearer)
            {
                token = await authManager.GetTokenAsync(http, UnderworldPlugin.GetPlayerDisplayName()).ConfigureAwait(false);
                if (string.IsNullOrEmpty(token))
                {
                    return null;
                }
            }

            using (HttpRequestMessage request = new HttpRequestMessage(method, BuildUrl(path)))
            {
                if (useBearer)
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }

                if (body != null)
                {
                    string json = JsonConvert.SerializeObject(body);
                    request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                }

                using (HttpResponseMessage response = await http.SendAsync(request).ConfigureAwait(false))
                {
                    if (response.StatusCode == HttpStatusCode.Unauthorized && useBearer && retryOnUnauthorized)
                    {
                        await authManager.RefreshOrRegisterTokenAsync(http, UnderworldPlugin.GetPlayerDisplayName()).ConfigureAwait(false);
                        return await SendCoreAsync<T>(path, method, body, useBearer, retryOnUnauthorized: false).ConfigureAwait(false);
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        string detail = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        throw new InvalidOperationException($"HTTP {(int)response.StatusCode} for {path}: {detail}");
                    }

                    string payload = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    return JsonConvert.DeserializeObject<T>(payload);
                }
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
    }
}

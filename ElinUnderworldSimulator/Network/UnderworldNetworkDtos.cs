using System.Collections.Generic;

using Newtonsoft.Json;

namespace ElinUnderworldSimulator
{
    internal sealed class UnderworldRegisterRequest
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

    internal sealed class UnderworldRegisterResponse
    {
        [JsonProperty("player_id")]
        public int PlayerId { get; set; }

        [JsonProperty("auth_token")]
        public string AuthToken { get; set; }

        [JsonProperty("display_name")]
        public string DisplayName { get; set; }

        [JsonProperty("underworld_rank")]
        public int UnderworldRank { get; set; }
    }

    internal sealed class UnderworldLoginRequest
    {
        [JsonProperty("auth_token")]
        public string AuthToken { get; set; }
    }

    internal sealed class UnderworldLoginResponse
    {
        [JsonProperty("player_id")]
        public int PlayerId { get; set; }

        [JsonProperty("display_name")]
        public string DisplayName { get; set; }

        [JsonProperty("underworld_rank")]
        public int UnderworldRank { get; set; }

        [JsonProperty("total_rep")]
        public int TotalRep { get; set; }

        [JsonProperty("faction_id")]
        public int? FactionId { get; set; }
    }

    internal sealed class UnderworldOrderDto
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("territory_id")]
        public string TerritoryId { get; set; }

        [JsonProperty("territory_name")]
        public string TerritoryName { get; set; }

        [JsonProperty("client_type")]
        public string ClientType { get; set; }

        [JsonProperty("client_name")]
        public string ClientName { get; set; }

        [JsonProperty("product_type")]
        public string ProductType { get; set; }

        [JsonProperty("product_id")]
        public string ProductId { get; set; }

        [JsonProperty("min_quantity")]
        public int MinQuantity { get; set; }

        [JsonProperty("max_quantity")]
        public int MaxQuantity { get; set; }

        [JsonProperty("min_potency")]
        public int MinPotency { get; set; }

        [JsonProperty("max_toxicity")]
        public int MaxToxicity { get; set; }

        [JsonProperty("base_payout")]
        public int BasePayout { get; set; }

        [JsonProperty("deadline_hours")]
        public int DeadlineHours { get; set; }

        [JsonProperty("created_at")]
        public string CreatedAt { get; set; }

        [JsonProperty("expires_at")]
        public string ExpiresAt { get; set; }
    }

    internal sealed class UnderworldOrderListResponse
    {
        [JsonProperty("orders")]
        public List<UnderworldOrderDto> Orders { get; set; } = new List<UnderworldOrderDto>();
    }

    internal sealed class UnderworldAcceptOrderRequest
    {
        [JsonProperty("order_id")]
        public int OrderId { get; set; }
    }

    internal sealed class UnderworldAcceptOrderResponse
    {
        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("order_id")]
        public int OrderId { get; set; }

        [JsonProperty("deadline")]
        public string Deadline { get; set; }
    }

    internal sealed class UnderworldShipmentSubmitRequest
    {
        [JsonProperty("order_id")]
        public int OrderId { get; set; }

        [JsonProperty("quantity")]
        public int Quantity { get; set; }

        [JsonProperty("avg_potency")]
        public int AvgPotency { get; set; }

        [JsonProperty("avg_toxicity")]
        public int AvgToxicity { get; set; }

        [JsonProperty("avg_traceability")]
        public int AvgTraceability { get; set; }

        [JsonProperty("item_ids")]
        public List<string> ItemIds { get; set; } = new List<string>();
    }

    internal sealed class UnderworldEnforcementEventDto
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("confiscated_quantity")]
        public int? ConfiscatedQuantity { get; set; }

        [JsonProperty("gold_penalty")]
        public int? GoldPenalty { get; set; }

        [JsonProperty("investigation_days")]
        public int? InvestigationDays { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }
    }

    internal sealed class UnderworldShipmentResultDto
    {
        [JsonProperty("shipment_id")]
        public int ShipmentId { get; set; }

        [JsonProperty("order_id")]
        public int OrderId { get; set; }

        [JsonProperty("outcome")]
        public string Outcome { get; set; }

        [JsonProperty("satisfaction_score")]
        public float SatisfactionScore { get; set; }

        [JsonProperty("final_payout")]
        public int FinalPayout { get; set; }

        [JsonProperty("heat_delta")]
        public int HeatDelta { get; set; }

        [JsonProperty("rep_delta")]
        public int RepDelta { get; set; }

        [JsonProperty("enforcement_event")]
        public UnderworldEnforcementEventDto EnforcementEvent { get; set; }

        [JsonProperty("territory_heat_after")]
        public int TerritoryHeatAfter { get; set; }
    }

    internal sealed class UnderworldShipmentSubmitResponse
    {
        [JsonProperty("shipment_id")]
        public int ShipmentId { get; set; }

        [JsonProperty("outcome")]
        public string Outcome { get; set; }

        [JsonProperty("satisfaction_score")]
        public float SatisfactionScore { get; set; }

        [JsonProperty("final_payout")]
        public int FinalPayout { get; set; }

        [JsonProperty("heat_delta")]
        public int HeatDelta { get; set; }

        [JsonProperty("rep_delta")]
        public int RepDelta { get; set; }

        [JsonProperty("enforcement_event")]
        public UnderworldEnforcementEventDto EnforcementEvent { get; set; }

        [JsonProperty("territory_heat_after")]
        public int TerritoryHeatAfter { get; set; }
    }

    internal sealed class UnderworldTerritoryChangeDto
    {
        [JsonProperty("territory_id")]
        public string TerritoryId { get; set; }

        [JsonProperty("old_faction")]
        public string OldFaction { get; set; }

        [JsonProperty("new_faction")]
        public string NewFaction { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }
    }

    internal sealed class UnderworldRankChangeDto
    {
        [JsonProperty("old_rank")]
        public int OldRank { get; set; }

        [JsonProperty("new_rank")]
        public int NewRank { get; set; }

        [JsonProperty("old_rank_name")]
        public string OldRankName { get; set; }

        [JsonProperty("new_rank_name")]
        public string NewRankName { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }
    }

    internal sealed class UnderworldShipmentResultsResponse
    {
        [JsonProperty("results")]
        public List<UnderworldShipmentResultDto> Results { get; set; } = new List<UnderworldShipmentResultDto>();

        [JsonProperty("territory_changes")]
        public List<UnderworldTerritoryChangeDto> TerritoryChanges { get; set; } = new List<UnderworldTerritoryChangeDto>();

        [JsonProperty("rank_change")]
        public UnderworldRankChangeDto RankChange { get; set; }
    }

    internal sealed class UnderworldTerritoryDto
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("heat")]
        public int Heat { get; set; }

        [JsonProperty("heat_level")]
        public string HeatLevel { get; set; }

        [JsonProperty("controlling_faction")]
        public string ControllingFaction { get; set; }

        [JsonProperty("controlling_faction_id")]
        public int? ControllingFactionId { get; set; }

        [JsonProperty("available_orders_count")]
        public int AvailableOrdersCount { get; set; }
    }

    internal sealed class UnderworldTerritoriesResponse
    {
        [JsonProperty("territories")]
        public List<UnderworldTerritoryDto> Territories { get; set; } = new List<UnderworldTerritoryDto>();
    }

    internal sealed class UnderworldCreateFactionRequest
    {
        [JsonProperty("name")]
        public string Name { get; set; }
    }

    internal sealed class UnderworldCreateFactionResponse
    {
        [JsonProperty("faction_id")]
        public int FactionId { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }
    }

    internal sealed class UnderworldJoinFactionRequest
    {
        [JsonProperty("faction_id")]
        public int FactionId { get; set; }
    }

    internal sealed class UnderworldJoinFactionResponse
    {
        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("faction_name")]
        public string FactionName { get; set; }
    }

    internal sealed class UnderworldFactionMemberDto
    {
        [JsonProperty("display_name")]
        public string DisplayName { get; set; }

        [JsonProperty("role")]
        public string Role { get; set; }

        [JsonProperty("rank")]
        public int Rank { get; set; }
    }

    internal sealed class UnderworldFactionDetailDto
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("leader")]
        public string Leader { get; set; }

        [JsonProperty("member_count")]
        public int MemberCount { get; set; }

        [JsonProperty("max_members")]
        public int MaxMembers { get; set; }

        [JsonProperty("controlled_territories")]
        public List<string> ControlledTerritories { get; set; } = new List<string>();

        [JsonProperty("members")]
        public List<UnderworldFactionMemberDto> Members { get; set; } = new List<UnderworldFactionMemberDto>();
    }

    internal sealed class UnderworldPlayerFactionDto
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("role")]
        public string Role { get; set; }
    }

    internal sealed class UnderworldPlayerStatusDto
    {
        [JsonProperty("player_id")]
        public int PlayerId { get; set; }

        [JsonProperty("display_name")]
        public string DisplayName { get; set; }

        [JsonProperty("underworld_rank")]
        public int UnderworldRank { get; set; }

        [JsonProperty("rank_name")]
        public string RankName { get; set; }

        [JsonProperty("total_rep")]
        public int TotalRep { get; set; }

        [JsonProperty("gold")]
        public int Gold { get; set; }

        [JsonProperty("faction")]
        public UnderworldPlayerFactionDto Faction { get; set; }

        [JsonProperty("reputation_by_territory")]
        public Dictionary<string, int> ReputationByTerritory { get; set; } = new Dictionary<string, int>();

        [JsonProperty("active_orders_count")]
        public int ActiveOrdersCount { get; set; }

        [JsonProperty("pending_shipments_count")]
        public int PendingShipmentsCount { get; set; }
    }

    internal sealed class UnderworldLeaveFactionResponse
    {
        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("faction_name")]
        public string FactionName { get; set; }
    }

    internal sealed class UnderworldDisbandFactionResponse
    {
        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("faction_name")]
        public string FactionName { get; set; }
    }

    internal sealed class UnderworldPromoteMemberRequest
    {
        [JsonProperty("player_id")]
        public int PlayerId { get; set; }
    }

    internal sealed class UnderworldPromoteMemberResponse
    {
        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("player_display_name")]
        public string PlayerDisplayName { get; set; }

        [JsonProperty("new_role")]
        public string NewRole { get; set; }
    }
}

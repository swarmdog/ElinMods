using System.Collections.Generic;
using Newtonsoft.Json;

namespace ElinUnderworldSimulator
{
    internal sealed class UnderworldSaveData
    {
        [JsonProperty]
        public int CurrentNerve = 4;

        [JsonProperty]
        public int MaxNerve = 6;

        [JsonProperty]
        public int LastNerveRaw;

        [JsonProperty]
        public Dictionary<string, CustomerState> Customers = new Dictionary<string, CustomerState>();

        [JsonProperty]
        public Dictionary<string, ZoneHeatState> ZoneHeat = new Dictionary<string, ZoneHeatState>();
    }

    internal sealed class CustomerState
    {
        [JsonProperty]
        public string CustomerKey;

        [JsonProperty]
        public string CustomerId;

        [JsonProperty]
        public string DisplayName;

        [JsonProperty]
        public string ZoneId;

        [JsonProperty]
        public string ZoneName;

        [JsonProperty]
        public string PreferredProductId;

        [JsonProperty]
        public int Loyalty;

        [JsonProperty]
        public int Addiction;

        [JsonProperty]
        public int Tolerance;

        [JsonProperty]
        public int PendingOrderQty;

        [JsonProperty]
        public int PendingOrderPricePct = 100;

        [JsonProperty]
        public int PendingOrderMinPotency;

        [JsonProperty]
        public int LastServedRaw;

        [JsonProperty]
        public int LastSampleRaw;

        [JsonProperty]
        public int LastOrderGeneratedRaw;

        [JsonProperty]
        public int ActiveOverdoseStage;

        [JsonProperty]
        public string LastOutcome;
    }

    internal sealed class ZoneHeatState
    {
        [JsonProperty]
        public int Value;

        [JsonProperty]
        public int LastRaw;
    }
}

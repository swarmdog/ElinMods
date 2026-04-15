using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace ElinUnderworldSimulator
{
    internal static class UnderworldRuntime
    {
        private const string SaveFileName = "underworld.json";
        private const int NerveRecoverMinutes = 240;
        private const int HeatDecayMinutes = 1440;

        internal static UnderworldSaveData Data { get; private set; } = new UnderworldSaveData();

        internal static void Initialize()
        {
            Data = new UnderworldSaveData();
        }

        internal static void ResetForNewGame()
        {
            Data = new UnderworldSaveData
            {
                CurrentNerve = 4,
                MaxNerve = 6,
                LastNerveRaw = EClass.world?.date?.GetRaw() ?? 0,
            };
        }

        internal static void Save()
        {
            string path = GetSavePath();
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? GameIO.pathCurrentSave);
            string json = JsonConvert.SerializeObject(Data, Formatting.Indented);
            File.WriteAllText(path, json);
            UnderworldPlugin.Log($"Saved underworld data to {path}.");
        }

        internal static void Load()
        {
            string path = GetSavePath();
            if (!File.Exists(path))
            {
                Data = new UnderworldSaveData
                {
                    LastNerveRaw = EClass.world?.date?.GetRaw() ?? 0,
                };
                return;
            }

            Data = JsonConvert.DeserializeObject<UnderworldSaveData>(File.ReadAllText(path)) ?? new UnderworldSaveData();
            if (Data.Customers == null)
            {
                Data.Customers = new Dictionary<string, CustomerState>();
            }

            if (Data.ZoneHeat == null)
            {
                Data.ZoneHeat = new Dictionary<string, ZoneHeatState>();
            }

            SyncNerve();
        }

        internal static int SyncNerve()
        {
            if (EClass.world?.date == null)
            {
                return Data.CurrentNerve;
            }

            int now = EClass.world.date.GetRaw();
            if (Data.LastNerveRaw <= 0)
            {
                Data.LastNerveRaw = now;
                return Data.CurrentNerve;
            }

            int elapsed = Math.Max(0, now - Data.LastNerveRaw);
            int recovered = elapsed / NerveRecoverMinutes;
            if (recovered > 0)
            {
                Data.CurrentNerve = Math.Min(Data.MaxNerve, Data.CurrentNerve + recovered);
                Data.LastNerveRaw += recovered * NerveRecoverMinutes;
            }

            return Data.CurrentNerve;
        }

        internal static bool SpendNerve(int amount)
        {
            SyncNerve();
            if (Data.CurrentNerve < amount)
            {
                return false;
            }

            Data.CurrentNerve -= amount;
            return true;
        }

        internal static int GetZoneHeat(Zone zone)
        {
            if (zone == null)
            {
                return 0;
            }

            ZoneHeatState state = GetOrCreateHeat(GetZoneKey(zone));
            ApplyHeatDecay(state);
            return state.Value;
        }

        internal static void AddZoneHeat(Zone zone, int amount)
        {
            if (zone == null || amount == 0)
            {
                return;
            }

            ZoneHeatState state = GetOrCreateHeat(GetZoneKey(zone));
            ApplyHeatDecay(state);
            state.Value = Math.Max(0, state.Value + amount);
            state.LastRaw = EClass.world?.date?.GetRaw() ?? state.LastRaw;
        }

        internal static CustomerState GetCustomer(Chara customer, bool create)
        {
            if (customer == null)
            {
                return null;
            }

            string key = BuildCustomerKey(customer);
            if (!Data.Customers.TryGetValue(key, out CustomerState state) && create)
            {
                state = new CustomerState
                {
                    CustomerKey = key,
                    CustomerId = customer.id,
                    DisplayName = customer.Name,
                    ZoneId = customer.homeZone?.id ?? EClass._zone?.id ?? string.Empty,
                    ZoneName = customer.homeZone?.Name ?? EClass._zone?.Name ?? "Unknown",
                };
                Data.Customers[key] = state;
            }

            if (state != null)
            {
                state.CustomerId = customer.id;
                state.DisplayName = customer.Name;
                state.ZoneId = customer.homeZone?.id ?? EClass._zone?.id ?? state.ZoneId;
                state.ZoneName = customer.homeZone?.Name ?? EClass._zone?.Name ?? state.ZoneName;
            }

            return state;
        }

        internal static IEnumerable<CustomerState> ListCustomers()
        {
            SyncNerve();
            foreach (ZoneHeatState state in Data.ZoneHeat.Values)
            {
                ApplyHeatDecay(state);
            }

            return Data.Customers.Values;
        }

        internal static int GetWithdrawalStage(CustomerState state)
        {
            if (state == null || state.Addiction <= 0 || state.LastServedRaw <= 0 || EClass.world?.date == null)
            {
                return 0;
            }

            int elapsed = EClass.world.date.GetRaw() - state.LastServedRaw;
            int threshold = Math.Max(360, 1440 - state.Addiction * 90);
            if (elapsed < threshold)
            {
                return 0;
            }

            int stage = 1 + (elapsed - threshold) / 1440;
            return Math.Min(3, stage);
        }

        private static string GetSavePath()
        {
            return Path.Combine(GameIO.pathCurrentSave, SaveFileName);
        }

        private static string BuildCustomerKey(Chara customer)
        {
            if (customer.uid != 0)
            {
                return customer.uid.ToString();
            }

            return $"{customer.id}:{customer.Name}:{customer.homeZone?.id ?? EClass._zone?.id ?? "zone"}";
        }

        private static string GetZoneKey(Zone zone)
        {
            if (zone.uid != 0)
            {
                return zone.uid.ToString();
            }

            return zone.id ?? "zone";
        }

        private static ZoneHeatState GetOrCreateHeat(string key)
        {
            if (!Data.ZoneHeat.TryGetValue(key, out ZoneHeatState state))
            {
                state = new ZoneHeatState
                {
                    LastRaw = EClass.world?.date?.GetRaw() ?? 0,
                };
                Data.ZoneHeat[key] = state;
            }

            return state;
        }

        private static void ApplyHeatDecay(ZoneHeatState state)
        {
            if (state == null || EClass.world?.date == null)
            {
                return;
            }

            if (state.LastRaw <= 0)
            {
                state.LastRaw = EClass.world.date.GetRaw();
                return;
            }

            int now = EClass.world.date.GetRaw();
            int elapsed = Math.Max(0, now - state.LastRaw);
            int decay = elapsed / HeatDecayMinutes;
            if (decay <= 0)
            {
                return;
            }

            state.Value = Math.Max(0, state.Value - decay);
            state.LastRaw += decay * HeatDecayMinutes;
        }
    }
}

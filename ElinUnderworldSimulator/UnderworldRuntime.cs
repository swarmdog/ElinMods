using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace ElinUnderworldSimulator
{
    internal static class UnderworldRuntime
    {
        private const string SaveFileName = "underworld.json";
        private const int NerveRecoverMinutes = 240;
        private const int HeatDecayMinutes = 1440;
        private const int PlayerAddictionDecayMinutes = 1440;
        private const int PlayerWithdrawalThreshold = 30;
        private const int ToleranceDecayMinutes = 4320;
        private const int OverdoseMildDuration = 240;
        private const int OverdoseModerateDuration = 480;
        private const int OverdoseSevereDuration = 720;

        private static bool _contrabandDirty = true;
        private static bool _hasExposedContraband;
        private static bool _contrabandDetected;
        private static int _lastDetectionZoneUid = -1;
        private static int _lastDetectionRaw;

        internal static UnderworldSaveData Data { get; private set; } = new UnderworldSaveData();

        internal static bool IsContrabandDetected => _contrabandDetected;

        internal static void Initialize()
        {
            Data = new UnderworldSaveData();
            ResetTransientState();
        }

        internal static void ResetForNewGame()
        {
            int raw = EClass.world?.date?.GetRaw() ?? 0;
            Data = new UnderworldSaveData
            {
                CurrentNerve = 4,
                MaxNerve = 6,
                LastNerveRaw = raw,
                LastPlayerAddictionDecayRaw = raw,
                LastProcessedWorldRaw = raw,
            };
            ResetTransientState();
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
            ResetTransientState();
            if (!File.Exists(path))
            {
                ResetForNewGame();
                return;
            }

            Data = JsonConvert.DeserializeObject<UnderworldSaveData>(File.ReadAllText(path)) ?? new UnderworldSaveData();
            Data.Customers = Data.Customers ?? new Dictionary<string, CustomerState>();
            Data.ZoneHeat = Data.ZoneHeat ?? new Dictionary<string, ZoneHeatState>();

            int raw = EClass.world?.date?.GetRaw() ?? 0;
            if (Data.LastNerveRaw <= 0)
            {
                Data.LastNerveRaw = raw;
            }

            if (Data.LastPlayerAddictionDecayRaw <= 0)
            {
                Data.LastPlayerAddictionDecayRaw = raw;
            }

            if (Data.LastProcessedWorldRaw <= 0)
            {
                Data.LastProcessedWorldRaw = raw;
            }

            foreach (CustomerState customer in Data.Customers.Values)
            {
                NormalizeCustomerState(customer, raw);
            }

            SyncNerve();
            ProcessWorldTick();
            OnZoneVisited(EClass._zone);
        }

        internal static void ProcessWorldTick()
        {
            if (EClass.world?.date == null)
            {
                return;
            }

            int now = EClass.world.date.GetRaw();
            SyncNerve();
            if (Data.LastProcessedWorldRaw == now)
            {
                return;
            }

            if (Data.LastProcessedWorldRaw <= 0)
            {
                Data.LastProcessedWorldRaw = now;
            }

            DecayPlayerAddiction(now);
            RefreshPlayerWithdrawal();

            foreach (CustomerState state in Data.Customers.Values)
            {
                NormalizeCustomerState(state, now);
                ApplyCustomerDecay(state, now);
            }

            SyncZoneCustomers(EClass._zone);
            ProcessContrabandDetectionTick(now);
            Data.LastProcessedWorldRaw = now;
        }

        internal static void OnZoneVisited(Zone zone)
        {
            if (zone == null)
            {
                return;
            }

            ProcessWorldTick();
            int now = EClass.world?.date?.GetRaw() ?? 0;
            foreach (CustomerState state in Data.Customers.Values)
            {
                if (state.IsDead || !BelongsToZone(state, zone))
                {
                    continue;
                }

                NormalizeCustomerState(state, now);
                if (state.LastServedRaw <= 0 || state.LastServedRaw < now)
                {
                    state.VisitsSinceService = Math.Min(99, state.VisitsSinceService + 1);
                }
            }

            UnderworldDealService.GenerateStandingOrdersForZone(zone);
            SyncZoneCustomers(zone);
            RollContrabandDetection(force: true);
            TryDevotedReferrals(zone);
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

        internal static int GetTerritoryRep(Zone zone)
        {
            if (zone == null)
            {
                return 0;
            }

            string key = GetZoneKey(zone);
            return Data.TerritoryRep.TryGetValue(key, out int rep) ? rep : 0;
        }

        // TODO: multiplayer — sync TerritoryRep with server reputation table
        internal static void ModTerritoryRep(Zone zone, int delta)
        {
            if (zone == null || delta == 0)
            {
                return;
            }

            string key = GetZoneKey(zone);
            Data.TerritoryRep.TryGetValue(key, out int current);
            Data.TerritoryRep[key] = Math.Max(0, current + delta);
        }

        internal static void RegisterDrugUse(Thing product)
        {
            if (!UnderworldConfig.EnablePCSelfAddiction.Value || product == null)
            {
                ClearCrashConditionForProduct(product);
                return;
            }

            ClearCrashConditionForProduct(product);
            Data.PlayerAddiction += 1 + GetProductPotency(product) / 20;
            Data.LastPlayerAddictionDecayRaw = EClass.world?.date?.GetRaw() ?? Data.LastPlayerAddictionDecayRaw;
            if (EClass.pc != null && EClass.pc.HasCondition<ConUWWithdrawal>())
            {
                EClass.pc.RemoveCondition<ConUWWithdrawal>();
            }
        }

        internal static void ReducePlayerAddiction(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            Data.PlayerAddiction = Math.Max(0, Data.PlayerAddiction - amount);
            RefreshPlayerWithdrawal();
        }

        internal static CustomerState GetCustomer(Chara customer, bool create)
        {
            if (customer == null)
            {
                return null;
            }

            string key = BuildCustomerKey(customer);
            if (!Data.Customers.TryGetValue(key, out CustomerState state))
            {
                if (!create)
                {
                    return null;
                }

                state = CreateCustomerState(customer, key);
                Data.Customers[key] = state;
                return state;
            }

            NormalizeCustomerState(state, EClass.world?.date?.GetRaw() ?? 0);
            if (state.IsDead)
            {
                if (!create)
                {
                    return null;
                }

                ResetRelationshipAfterDeath(state);
            }

            RefreshCustomerIdentity(state, customer);
            return state;
        }

        internal static IEnumerable<CustomerState> ListCustomers()
        {
            ProcessWorldTick();
            foreach (ZoneHeatState state in Data.ZoneHeat.Values)
            {
                ApplyHeatDecay(state);
            }

            return Data.Customers.Values;
        }

        internal static int GetWithdrawalStage(CustomerState state)
        {
            if (state == null || state.IsDead || state.Addiction < 31)
            {
                return 0;
            }

            int threshold;
            int baseStage;
            if (state.Addiction >= 86)
            {
                threshold = 1;
                baseStage = 3;
            }
            else if (state.Addiction >= 61)
            {
                threshold = 2;
                baseStage = 2;
            }
            else
            {
                threshold = 3;
                baseStage = 1;
            }

            if (state.VisitsSinceService < threshold)
            {
                return 0;
            }

            return Math.Min(3, baseStage + state.VisitsSinceService - threshold);
        }

        internal static bool HasOfferCooldown(CustomerState state)
        {
            return GetOfferCooldownRemainingRaw(state) > 0;
        }

        internal static int GetOfferCooldownRemainingHours(CustomerState state)
        {
            int remainingRaw = GetOfferCooldownRemainingRaw(state);
            return remainingRaw <= 0 ? 0 : (remainingRaw + 59) / 60;
        }

        internal static void SetOfferCooldown(CustomerState state, int hours)
        {
            if (state == null)
            {
                return;
            }

            int now = EClass.world?.date?.GetRaw() ?? 0;
            state.CooldownExpiresRaw = now + Math.Max(0, hours) * 60;
        }

        internal static void ClearOfferCooldown(CustomerState state)
        {
            if (state != null)
            {
                state.CooldownExpiresRaw = 0;
            }
        }

        internal static void MarkCustomerServed(CustomerState state, int now)
        {
            if (state == null)
            {
                return;
            }

            state.LastServedRaw = now;
            state.LastToleranceDecayRaw = now;
            state.LastAddictionDecayRaw = now;
            state.VisitsSinceService = 0;
            state.ActiveOverdoseStage = 0;
        }

        internal static void SyncCustomerState(Chara customer, CustomerState state)
        {
            if (customer == null || state == null || customer.IsPC || customer.IsPCParty || customer.IsPCFaction || customer.IsPCFactionMinion)
            {
                return;
            }

            RefreshCustomerIdentity(state, customer);
            state.LastKnownArchetype = UnderworldArchetypeService.Classify(customer);
            customer.elements.SetBase(UnderworldContentIds.CustomerAddictionElement, state.Addiction);
            customer.elements.SetBase(UnderworldContentIds.CustomerToleranceElement, state.Tolerance);
            customer.elements.SetBase(UnderworldContentIds.CustomerLoyaltyElement, state.Loyalty);
            customer.elements.SetBase(UnderworldContentIds.CustomerPreferredProductElement, UnderworldDrugCatalog.GetProductMarker(state.PreferredProductId));
            customer.elements.SetBase(UnderworldContentIds.CustomerOfferCooldownElement, GetOfferCooldownRemainingHours(state));
            SyncWithdrawalCondition(customer, state);
            SyncOverdoseCondition(customer, state);
        }

        internal static void RecordFatalOverdose(Chara customer, CustomerState state, string summary)
        {
            if (state == null)
            {
                return;
            }

            state.IsDead = true;
            state.LastDeathRaw = EClass.world?.date?.GetRaw() ?? state.LastDeathRaw;
            state.DeathSummary = summary;
            state.LastOutcome = summary;
            state.PendingOrderQty = 0;
            state.PendingOrderPricePct = 100;
            state.PendingOrderMinPotency = 0;
            state.ActiveOverdoseStage = 0;
            state.CooldownExpiresRaw = 0;
            state.PreferredProductId = string.Empty;
            state.Loyalty = 0;
            state.Addiction = 0;
            state.Tolerance = 0;
            state.VisitsSinceService = 0;
            if (customer != null)
            {
                customer.RemoveCondition<ConUWOverdose>();
                customer.RemoveCondition<ConUWWithdrawal>();
            }
        }

        internal static void InvalidateContrabandState()
        {
            _contrabandDirty = true;
        }

        internal static bool HasExposedContrabandCached()
        {
            if (_contrabandDirty)
            {
                RefreshContrabandExposure();
            }

            return _hasExposedContraband;
        }

        internal static bool RefreshContrabandExposure()
        {
            _hasExposedContraband = false;
            _contrabandDirty = false;
            if (EClass.pc == null)
            {
                return false;
            }

            foreach (Thing thing in EClass.pc.things.List(t => UnderworldDrugCatalog.IsContraband(t.id), onlyAccessible: true))
            {
                if (!IsInsideSampleKit(thing))
                {
                    _hasExposedContraband = true;
                    break;
                }
            }

            return _hasExposedContraband;
        }

        internal static void ClearContrabandDetection()
        {
            SetContrabandDetected(false);
            _lastDetectionZoneUid = EClass._zone?.uid ?? -1;
            _lastDetectionRaw = EClass.world?.date?.GetRaw() ?? 0;
        }

        internal static void RollContrabandDetection(bool force = false)
        {
            int now = EClass.world?.date?.GetRaw() ?? 0;
            int zoneUid = EClass._zone?.uid ?? -1;
            if (!IsLawfulZone(EClass._zone))
            {
                _lastDetectionZoneUid = zoneUid;
                _lastDetectionRaw = now;
                ClearContrabandDetection();
                return;
            }

            bool hasExposedContraband = RefreshContrabandExposure();
            if (!hasExposedContraband)
            {
                _lastDetectionZoneUid = zoneUid;
                _lastDetectionRaw = now;
                ClearContrabandDetection();
                return;
            }

            if (!force && zoneUid == _lastDetectionZoneUid && now - _lastDetectionRaw < 60)
            {
                return;
            }

            int stealth = EClass.pc?.Evalue(152) ?? 0;
            int negotiation = EClass.pc?.Evalue(291) ?? 0;
            float chance = UnderworldConfig.ContrabandDetectionBase.Value
                - stealth * UnderworldConfig.ContrabandStealthReduction.Value
                - negotiation * UnderworldConfig.ContrabandNegotiationReduction.Value;
            chance = Math.Max(UnderworldConfig.ContrabandDetectionFloor.Value, Math.Min(UnderworldConfig.ContrabandDetectionCeiling.Value, chance));
            bool detected = EClass.rnd(100) < chance;
            SetContrabandDetected(detected);
            if (EClass.pc != null)
            {
                EClass.pc.ModExp(152, detected ? 15 : 5);
            }

            _lastDetectionZoneUid = zoneUid;
            _lastDetectionRaw = now;
        }

        internal static void HandleInventoryMutation(Thing thing)
        {
            if (thing == null || EClass.pc == null || !ThingMayAffectContrabandState(thing))
            {
                return;
            }

            bool previousExposed = HasExposedContrabandCached();
            InvalidateContrabandState();
            bool isExposed = RefreshContrabandExposure();
            if (!isExposed)
            {
                ClearContrabandDetection();
                return;
            }

            if (IsLawfulZone(EClass._zone))
            {
                RollContrabandDetection(force: true);
            }
        }

        internal static bool IsInsideSampleKit(Thing thing)
        {
            Card parent = thing?.parent as Card;
            while (parent != null)
            {
                if (parent is Thing parentThing && parentThing.trait is TraitSampleKit)
                {
                    return true;
                }

                parent = parent.parent as Card;
            }

            return false;
        }

        internal static bool IsLawfulZone(Zone zone)
        {
            return zone != null && zone.HasLaw && !zone.AllowCriminal && !zone.IsPCFaction;
        }

        internal static int GetProductPotency(Thing product)
        {
            if (product == null)
            {
                return 0;
            }

            int potency = product.Evalue(UnderworldContentIds.PotencyElement);
            if (potency > 0)
            {
                return potency;
            }

            return UnderworldDrugCatalog.TryGetProduct(product.id, out UnderworldProductDefinition definition) ? definition.BasePotency : 0;
        }

        internal static int GetProductToxicity(Thing product)
        {
            if (product == null)
            {
                return 0;
            }

            int toxicity = product.Evalue(UnderworldContentIds.ToxicityElement);
            if (toxicity > 0)
            {
                return toxicity;
            }

            return UnderworldDrugCatalog.TryGetProduct(product.id, out UnderworldProductDefinition definition) ? definition.BaseToxicity : 0;
        }

        internal static Chara FindLoadedCustomer(CustomerState state, Zone zone = null)
        {
            if (state == null || state.IsDead)
            {
                return null;
            }

            zone ??= EClass._zone;
            if (zone == null || zone != EClass._zone || EClass._map == null)
            {
                return null;
            }

            if (int.TryParse(state.CustomerKey, out int uid))
            {
                Chara byUid = zone.FindChara(uid);
                if (byUid != null && !byUid.isDestroyed)
                {
                    return byUid;
                }
            }

            return EClass._map.charas.FirstOrDefault(chara => chara != null && !chara.isDestroyed && !chara.IsPC && string.Equals(BuildCustomerKey(chara), state.CustomerKey, StringComparison.Ordinal));
        }

        internal static void ClearCrashConditionForProduct(Thing product)
        {
            if (product == null || EClass.pc == null)
            {
                return;
            }

            if (!UnderworldDrugCatalog.TryGetConsumptionProfile(product.id, out UnderworldConsumptionProfile profile)
                || string.IsNullOrEmpty(profile.CrashConditionId))
            {
                return;
            }

            switch (profile.CrashConditionId)
            {
                case UnderworldContentIds.ConShadowCrash:
                    RemoveCrashCondition<ConUWShadowCrash>();
                    break;
                case UnderworldContentIds.ConBerserkerCrash:
                    RemoveCrashCondition<ConUWBerserkerCrash>();
                    break;
                case UnderworldContentIds.ConRushCrash:
                    RemoveCrashCondition<ConUWRushCrash>();
                    break;
                default:
                    throw new InvalidOperationException($"Unhandled underworld crash condition alias '{profile.CrashConditionId}'.");
            }
        }

        private static void ResetTransientState()
        {
            _contrabandDirty = true;
            _hasExposedContraband = false;
            _contrabandDetected = false;
            _lastDetectionZoneUid = -1;
            _lastDetectionRaw = 0;
        }

        private static string GetSavePath()
        {
            return Path.Combine(GameIO.pathCurrentSave, SaveFileName);
        }

        private static void RemoveCrashCondition<T>() where T : Condition
        {
            if (EClass.pc.HasCondition<T>())
            {
                EClass.pc.RemoveCondition<T>();
            }
        }

        private static void DecayPlayerAddiction(int now)
        {
            if (Data.LastPlayerAddictionDecayRaw <= 0)
            {
                Data.LastPlayerAddictionDecayRaw = now;
                return;
            }

            int elapsed = Math.Max(0, now - Data.LastPlayerAddictionDecayRaw);
            int decay = elapsed / PlayerAddictionDecayMinutes;
            if (decay <= 0)
            {
                return;
            }

            Data.PlayerAddiction = Math.Max(0, Data.PlayerAddiction - decay);
            Data.LastPlayerAddictionDecayRaw += decay * PlayerAddictionDecayMinutes;
        }

        private static void RefreshPlayerWithdrawal()
        {
            if (!UnderworldConfig.EnablePCSelfAddiction.Value || EClass.pc == null)
            {
                if (EClass.pc != null && EClass.pc.HasCondition<ConUWWithdrawal>())
                {
                    EClass.pc.RemoveCondition<ConUWWithdrawal>();
                }
                return;
            }

            bool hasAnyDrugCondition = false;
            foreach (Condition condition in EClass.pc.conditions)
            {
                string alias = condition.source?.alias ?? condition.GetType().Name;
                if (alias.StartsWith("ConUW", StringComparison.Ordinal) && alias != UnderworldContentIds.ConWithdrawal)
                {
                    hasAnyDrugCondition = true;
                    break;
                }
            }

            if (Data.PlayerAddiction > PlayerWithdrawalThreshold && !hasAnyDrugCondition)
            {
                if (!EClass.pc.HasCondition<ConUWWithdrawal>())
                {
                    EClass.pc.AddCondition<ConUWWithdrawal>(200, force: true);
                }
            }
            else if (EClass.pc.HasCondition<ConUWWithdrawal>())
            {
                EClass.pc.RemoveCondition<ConUWWithdrawal>();
            }
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

        private static CustomerState CreateCustomerState(Chara customer, string key)
        {
            int raw = EClass.world?.date?.GetRaw() ?? 0;
            CustomerState state = new CustomerState
            {
                CustomerKey = key,
                LastToleranceDecayRaw = raw,
                LastAddictionDecayRaw = raw,
            };
            RefreshCustomerIdentity(state, customer);
            return state;
        }

        private static void RefreshCustomerIdentity(CustomerState state, Chara customer)
        {
            if (state == null || customer == null)
            {
                return;
            }

            state.CustomerId = customer.id;
            state.DisplayName = customer.Name;
            state.ZoneId = customer.homeZone?.id ?? EClass._zone?.id ?? state.ZoneId;
            state.ZoneName = customer.homeZone?.Name ?? EClass._zone?.Name ?? state.ZoneName;
            state.LastKnownArchetype = UnderworldArchetypeService.Classify(customer);
        }

        private static void NormalizeCustomerState(CustomerState state, int now)
        {
            if (state == null)
            {
                return;
            }

            state.CustomerKey = state.CustomerKey ?? string.Empty;
            state.CustomerId = state.CustomerId ?? string.Empty;
            state.DisplayName = state.DisplayName ?? "Unknown";
            state.ZoneId = state.ZoneId ?? string.Empty;
            state.ZoneName = state.ZoneName ?? "Unknown";
            state.PreferredProductId = state.PreferredProductId ?? string.Empty;
            state.LastOutcome = state.LastOutcome ?? string.Empty;
            state.DeathSummary = state.DeathSummary ?? string.Empty;
            state.Addiction = Math.Max(0, Math.Min(100, state.Addiction));
            state.Tolerance = Math.Max(0, Math.Min(50, state.Tolerance));
            state.Loyalty = Math.Max(0, state.Loyalty);
            state.PendingOrderQty = Math.Max(0, state.PendingOrderQty);
            state.PendingOrderPricePct = Math.Max(1, state.PendingOrderPricePct == 0 ? 100 : state.PendingOrderPricePct);
            state.PendingOrderMinPotency = Math.Max(0, state.PendingOrderMinPotency);
            state.ActiveOverdoseStage = Math.Max(0, Math.Min(3, state.ActiveOverdoseStage));
            state.VisitsSinceService = Math.Max(0, state.VisitsSinceService);
            if (state.LastToleranceDecayRaw <= 0)
            {
                state.LastToleranceDecayRaw = now;
            }

            if (state.LastAddictionDecayRaw <= 0)
            {
                state.LastAddictionDecayRaw = now;
            }
        }

        private static void ResetRelationshipAfterDeath(CustomerState state)
        {
            state.IsDead = false;
            state.PreferredProductId = string.Empty;
            state.Loyalty = 0;
            state.Addiction = 0;
            state.Tolerance = 0;
            state.PendingOrderQty = 0;
            state.PendingOrderPricePct = 100;
            state.PendingOrderMinPotency = 0;
            state.LastServedRaw = 0;
            state.LastSampleRaw = 0;
            state.LastOrderGeneratedRaw = 0;
            state.ActiveOverdoseStage = 0;
            state.CooldownExpiresRaw = 0;
            state.VisitsSinceService = 0;
            int now = EClass.world?.date?.GetRaw() ?? 0;
            state.LastToleranceDecayRaw = now;
            state.LastAddictionDecayRaw = now;
            state.LastOutcome = string.Empty;
        }

        private static void ApplyCustomerDecay(CustomerState state, int now)
        {
            if (state == null || state.IsDead)
            {
                return;
            }

            if (state.LastToleranceDecayRaw < state.LastServedRaw)
            {
                state.LastToleranceDecayRaw = state.LastServedRaw;
            }

            int toleranceElapsed = Math.Max(0, now - state.LastToleranceDecayRaw);
            int toleranceDecay = toleranceElapsed / ToleranceDecayMinutes;
            if (toleranceDecay > 0)
            {
                state.Tolerance = Math.Max(0, state.Tolerance - toleranceDecay);
                state.LastToleranceDecayRaw += toleranceDecay * ToleranceDecayMinutes;
            }

            if (!UnderworldConfig.AddictionNaturalDecayEnabled.Value)
            {
                return;
            }

            if (state.LastAddictionDecayRaw < state.LastServedRaw)
            {
                state.LastAddictionDecayRaw = state.LastServedRaw;
            }

            int addictionElapsed = Math.Max(0, now - state.LastAddictionDecayRaw);
            int addictionDecay = addictionElapsed / PlayerAddictionDecayMinutes;
            if (addictionDecay > 0)
            {
                state.Addiction = Math.Max(0, state.Addiction - addictionDecay);
                state.LastAddictionDecayRaw += addictionDecay * PlayerAddictionDecayMinutes;
            }

            // OD expiry — time-based cleanup so overdoses don't persist indefinitely
            if (state.ActiveOverdoseStage > 0 && state.OverdoseExpiresRaw > 0 && now >= state.OverdoseExpiresRaw)
            {
                state.ActiveOverdoseStage = 0;
                state.OverdoseExpiresRaw = 0;
            }

            // Hooked passive rep: +1 per in-game day
            if (state.Loyalty >= 15 && !string.IsNullOrEmpty(state.ZoneId))
            {
                if (state.LastPassiveRepRaw <= 0)
                {
                    state.LastPassiveRepRaw = now;
                }

                int daysSinceLastRep = (now - state.LastPassiveRepRaw) / 1440;
                if (daysSinceLastRep >= 1)
                {
                    string key = state.ZoneId;
                    Data.TerritoryRep.TryGetValue(key, out int current);
                    Data.TerritoryRep[key] = Math.Max(0, current + daysSinceLastRep);
                    state.LastPassiveRepRaw += daysSinceLastRep * 1440;
                }
            }
        }

        private static void SyncZoneCustomers(Zone zone)
        {
            if (zone == null || zone != EClass._zone || EClass._map == null)
            {
                return;
            }

            foreach (CustomerState state in Data.Customers.Values)
            {
                if (state.IsDead || !BelongsToZone(state, zone))
                {
                    continue;
                }

                Chara customer = FindLoadedCustomer(state, zone);
                if (customer != null && !customer.isDead)
                {
                    SyncCustomerState(customer, state);
                }
            }
        }

        private static bool BelongsToZone(CustomerState state, Zone zone)
        {
            if (state == null || zone == null)
            {
                return false;
            }

            return string.Equals(state.ZoneId, zone.id ?? string.Empty, StringComparison.Ordinal)
                || string.Equals(state.ZoneId, GetZoneKey(zone), StringComparison.Ordinal);
        }

        private static void SyncWithdrawalCondition(Chara customer, CustomerState state)
        {
            int stage = GetWithdrawalStage(state);
            if (stage <= 0 || customer.IsPCParty || customer.IsPCFaction || customer.IsPCFactionMinion)
            {
                customer.RemoveCondition<ConUWWithdrawal>();
                return;
            }

            ConUWWithdrawal condition = GetCondition<ConUWWithdrawal>(customer) ?? customer.AddCondition<ConUWWithdrawal>(100, force: true) as ConUWWithdrawal;
            if (condition == null)
            {
                return;
            }

            condition.refVal = stage - 1;
            condition.value = 999999;
            condition.SetPhase();
        }

        internal static void SyncOverdoseCondition(Chara customer, CustomerState state)
        {
            if (state.ActiveOverdoseStage <= 0)
            {
                customer.RemoveCondition<ConUWOverdose>();
                return;
            }

            int duration = state.ActiveOverdoseStage switch
            {
                1 => OverdoseMildDuration,
                2 => OverdoseModerateDuration,
                _ => OverdoseSevereDuration,
            };
            ConUWOverdose condition = GetCondition<ConUWOverdose>(customer) ?? customer.AddCondition<ConUWOverdose>(duration, force: true) as ConUWOverdose;
            if (condition == null)
            {
                return;
            }

            condition.refVal = state.ActiveOverdoseStage - 1; // 1→0, 2→1, 3→2
            condition.value = duration;
            condition.SetPhase();
        }

        private static T GetCondition<T>(Chara customer) where T : Condition
        {
            foreach (Condition condition in customer.conditions)
            {
                if (condition is T typed)
                {
                    return typed;
                }
            }

            return null;
        }

        private static void ProcessContrabandDetectionTick(int now)
        {
            if (!IsLawfulZone(EClass._zone))
            {
                ClearContrabandDetection();
                return;
            }

            if (_lastDetectionRaw <= 0 || now - _lastDetectionRaw >= 60 || (EClass._zone?.uid ?? -1) != _lastDetectionZoneUid)
            {
                RollContrabandDetection(force: true);
            }
        }

        private static void SetContrabandDetected(bool detected)
        {
            bool changed = _contrabandDetected != detected;
            _contrabandDetected = detected;
            if (changed && IsLawfulZone(EClass._zone))
            {
                EClass._zone.RefreshCriminal();
            }
        }

        private static int GetOfferCooldownRemainingRaw(CustomerState state)
        {
            if (state == null || state.CooldownExpiresRaw <= 0 || EClass.world?.date == null)
            {
                return 0;
            }

            return Math.Max(0, state.CooldownExpiresRaw - EClass.world.date.GetRaw());
        }

        private static bool ThingMayAffectContrabandState(Thing thing)
        {
            if (thing == null)
            {
                return false;
            }

            if (UnderworldDrugCatalog.IsContraband(thing.id) || thing.trait is TraitSampleKit)
            {
                return true;
            }

            foreach (Thing child in thing.things)
            {
                if (ThingMayAffectContrabandState(child))
                {
                    return true;
                }
            }

            return false;
        }

        private static void TryDevotedReferrals(Zone zone)
        {
            if (zone == null || EClass._map?.charas == null)
            {
                return;
            }

            foreach (CustomerState state in Data.Customers.Values)
            {
                if (state.IsDead || !BelongsToZone(state, zone) || state.Loyalty < 8)
                {
                    continue;
                }

                // 5% chance per visit that a Devoted+ customer refers a new prospect
                if (EClass.rnd(100) >= 5)
                {
                    continue;
                }

                Chara referral = FindEligibleReferral(zone);
                if (referral == null)
                {
                    continue;
                }

                CustomerState newState = GetCustomer(referral, create: true);
                newState.Loyalty = Math.Max(1, newState.Loyalty);
                newState.LastOutcome = $"{state.DisplayName} put in a good word.";
                // TODO: multiplayer — notify server of new prospect
                Msg.SayRaw($"{state.DisplayName} put in a word for you with {referral.Name}.");
            }
        }

        private static Chara FindEligibleReferral(Zone zone)
        {
            if (EClass._map?.charas == null)
            {
                return null;
            }

            foreach (Chara chara in EClass._map.charas)
            {
                if (chara == null || chara.IsPC || chara.IsPCParty || chara.trait is TraitGuard)
                {
                    continue;
                }

                if (!chara.IsHumanSpeak || chara.bio.IsUnderAge(chara))
                {
                    continue;
                }

                string key = BuildCustomerKey(chara);
                if (Data.Customers.ContainsKey(key))
                {
                    continue;
                }

                // Don't always pick the first eligible — randomize
                if (EClass.rnd(3) == 0)
                {
                    return chara;
                }
            }

            return null;
        }
    }
}

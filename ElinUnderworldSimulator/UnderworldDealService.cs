using System;
using System.Collections.Generic;
using System.Linq;

namespace ElinUnderworldSimulator
{
    internal static class UnderworldDealService
    {
        private enum LoyaltyTier
        {
            Prospect,
            Regular,
            Devoted,
            Hooked,
        }

        private sealed class RestockOffer
        {
            public string ItemId;
            public string Label;
            public int Quantity;
            public int Price;
        }

        private static readonly RestockOffer[] RestockOffers =
        {
            new RestockOffer { ItemId = UnderworldContentIds.HerbWhisperId, Label = "Whispervine bundle", Quantity = 4, Price = 18 },
            new RestockOffer { ItemId = UnderworldContentIds.HerbDreamId, Label = "Dreamblossom wrap", Quantity = 3, Price = 22 },
            new RestockOffer { ItemId = UnderworldContentIds.HerbShadowId, Label = "Shadowcap satchel", Quantity = 3, Price = 26 },
            new RestockOffer { ItemId = UnderworldContentIds.MineralCrudeId, Label = "Crude moonite pouch", Quantity = 2, Price = 28 },
            new RestockOffer { ItemId = UnderworldContentIds.HerbCrimsonId, Label = "Crimsonwort bundle", Quantity = 2, Price = 36 },
            new RestockOffer { ItemId = "potion_empty", Label = "Empty bottle crate", Quantity = 4, Price = 10 },
            new RestockOffer { ItemId = ModInfo.AntidoteId, Label = "Alchemist's Reprieve", Quantity = 1, Price = 35 },
        };

        internal static void AppendChoices(DramaCustomSequence sequence, Chara customer)
        {
            if (sequence == null || customer == null)
            {
                return;
            }

            if (customer.id == ModInfo.FixerId)
            {
                AppendFixerChoices(sequence);
                return;
            }

            AppendCustomerChoices(sequence, customer);
        }

        internal static void GenerateStandingOrdersForZone(Zone zone)
        {
            if (zone == null)
            {
                return;
            }

            foreach (CustomerState state in UnderworldRuntime.ListCustomers())
            {
                if (state.IsDead || state.Loyalty < 3 || state.PendingOrderQty > 0 || string.IsNullOrEmpty(state.PreferredProductId))
                {
                    continue;
                }

                if (!string.Equals(state.ZoneId, zone.id ?? string.Empty, StringComparison.Ordinal))
                {
                    continue;
                }

                GenerateStandingOrder(state);
            }
        }

        internal static void ShowLedger()
        {
            DealerLedgerDialog.Open();
        }

        internal static bool TryUseAntidoteOn(Card target)
        {
            if (!(target is Chara customer))
            {
                Msg.SayRaw("The antidote needs a living target.");
                return false;
            }

            CustomerState state = UnderworldRuntime.GetCustomer(customer, create: false);
            if (state == null || state.ActiveOverdoseStage <= 0 || !customer.HasCondition<ConUWOverdose>())
            {
                Msg.SayRaw($"{customer.Name} does not need an antidote right now.");
                return false;
            }

            bool severe = state.ActiveOverdoseStage >= 2;
            state.ActiveOverdoseStage = 0;
            if (severe)
            {
                state.Loyalty += 3;
            }

            state.LastOutcome = severe
                ? $"{customer.Name} survived a severe overdose after you intervened."
                : $"{customer.Name} steadied after an overdose.";
            customer.RemoveCondition<ConUWOverdose>();
            customer.HealHP(Math.Max(1, customer.MaxHP / 2));
            UnderworldRuntime.SyncCustomerState(customer, state);
            Msg.SayRaw(severe
                ? $"{customer.Name} claws back from the brink and locks eyes with you in stunned gratitude."
                : $"{customer.Name} steadies after the antidote takes hold.");
            return true;
        }

        private static void AppendFixerChoices(DramaCustomSequence sequence)
        {
            if (!UnderworldPlugin.IsUnderworldMode())
            {
                return;
            }

            sequence.Choice("Ask for the ground rules", ShowFixerBriefing);
            sequence.Choice("Buy a quiet restock", OpenRestockDialog);
            sequence.Choice("Review the local ledger", ShowLedger);
        }

        private static void AppendCustomerChoices(DramaCustomSequence sequence, Chara customer)
        {
            if (!CanApproach(customer))
            {
                return;
            }

            UnderworldRuntime.ProcessWorldTick();
            CustomerState state = UnderworldRuntime.GetCustomer(customer, create: false);
            if (state != null && state.Loyalty >= 3 && state.PendingOrderQty <= 0)
            {
                GenerateStandingOrder(state);
            }

            if (state == null)
            {
                if (!UnderworldRuntime.HasOfferCooldown(LookupDeadOrDormantCustomer(customer)))
                {
                    sequence.Choice("Offer a sample", () => OpenSampleDialog(customer));
                }
                return;
            }

            if (!UnderworldRuntime.HasOfferCooldown(state) && state.Loyalty <= 2)
            {
                sequence.Choice("Offer a fresh sample", () => OpenSampleDialog(customer));
            }

            if (state.PendingOrderQty > 0)
            {
                sequence.Choice("Fulfill their standing order", () => FulfillPendingOrder(customer));
            }

            sequence.Choice("Review their status", () => ShowCustomerStatus(customer));
        }

        private static bool CanApproach(Chara customer)
        {
            if (customer.IsPC || customer.IsPCParty || customer.IsPCFaction || customer.IsPCFactionMinion)
            {
                return false;
            }

            if (customer.trait is TraitGuard)
            {
                return false;
            }

            if (customer.bio.IsUnderAge(customer))
            {
                return false;
            }

            if (!customer.IsHumanSpeak || customer.c_bossType != BossType.none)
            {
                return false;
            }

            return !(EClass._zone?.IsRegion ?? true);
        }

        private static void OpenSampleDialog(Chara customer)
        {
            CustomerState existing = LookupDeadOrDormantCustomer(customer);
            if (UnderworldRuntime.HasOfferCooldown(existing))
            {
                Msg.SayRaw($"{customer.Name} is still keeping their distance.");
                return;
            }

            if (!UnderworldRuntime.SpendNerve(1))
            {
                Msg.SayRaw("Your nerve is shot. Wait a while before trying again.");
                return;
            }

            List<UnderworldProductDefinition> available = GetAvailableProducts(minimum: 1);
            if (available.Count == 0)
            {
                Msg.SayRaw("You do not have anything ready to sample.");
                return;
            }

            Dialog.Choice("Choose a sample to float.", dialog =>
            {
                foreach (UnderworldProductDefinition product in available)
                {
                    UnderworldProductDefinition chosen = product;
                    string label = $"{product.DisplayName} x{CountAccessibleProduct(product.ItemId)}";
                    dialog.AddButton(label, () => OfferSample(customer, chosen), close: true);
                }

                dialog.AddButton(Lang.Get("close"));
            });
        }

        private static void OfferSample(Chara customer, UnderworldProductDefinition product)
        {
            if (product == null)
            {
                return;
            }

            CustomerState state = UnderworldRuntime.GetCustomer(customer, create: true);
            NpcArchetype archetype = UnderworldArchetypeService.Classify(customer);
            bool guardNearby = GuardNearby();
            int chance = ComputeAcceptanceChance(archetype, guardNearby);

            MaybeTriggerNearbyGuardAlert(customer, severe: false);

            if (EClass.rnd(100) >= chance)
            {
                UnderworldRuntime.SetOfferCooldown(state, UnderworldConfig.RefusalCooldownHours.Value);
                state.LastOutcome = $"{customer.Name} turned down a sample of {product.DisplayName}.";
                customer.ModAffinity(EClass.pc, -2, show: false);
                UnderworldRuntime.AddZoneHeat(EClass._zone, 1);
                MaybeTriggerRefusalGuardCall(customer, archetype);
                UnderworldRuntime.SyncCustomerState(customer, state);
                Msg.SayRaw($"{customer.Name} reads the room, then waves the offer away.");
                return;
            }

            if (!TryConsumeProduct(product.ItemId, 1, minPotency: 0, out _, out _))
            {
                Msg.SayRaw("You ran out before the offer landed.");
                return;
            }

            state.PreferredProductId = product.ItemId;
            state.Loyalty = Math.Max(1, state.Loyalty);
            state.LastSampleRaw = EClass.world?.date?.GetRaw() ?? state.LastSampleRaw;
            state.LastKnownArchetype = archetype;
            UnderworldRuntime.ClearOfferCooldown(state);
            state.LastOutcome = $"{customer.Name} accepted a sample of {product.DisplayName}.";
            customer.ModAffinity(EClass.pc, 2 + EClass.rnd(2), show: false);
            EClass.pc.ModExp(291, 30);
            UnderworldRuntime.ModTerritoryRep(EClass._zone, UnderworldConfig.RepGainPerSample.Value);
            UnderworldRuntime.SyncCustomerState(customer, state);
            Msg.SayRaw($"{customer.Name} palms the sample and asks what else you can get.");
        }

        private static void FulfillPendingOrder(Chara customer)
        {
            CustomerState state = UnderworldRuntime.GetCustomer(customer, create: false);
            if (state == null)
            {
                Msg.SayRaw("They do not know you in that way yet.");
                return;
            }

            if (state.PendingOrderQty <= 0)
            {
                Msg.SayRaw($"{customer.Name} is not looking for a handoff right now.");
                return;
            }

            if (!UnderworldDrugCatalog.TryGetProduct(state.PreferredProductId, out UnderworldProductDefinition product))
            {
                Msg.SayRaw("Their usual order no longer makes sense.");
                return;
            }

            if (!UnderworldRuntime.SpendNerve(1))
            {
                Msg.SayRaw("You need time to steady yourself before making another handoff.");
                return;
            }

            int quantity = state.PendingOrderQty;
            if (!TryConsumeProduct(product.ItemId, quantity, state.PendingOrderMinPotency, out int potency, out int toxicity))
            {
                Msg.SayRaw($"You need {quantity} {product.DisplayName} with at least {state.PendingOrderMinPotency} potency to close this order.");
                return;
            }

            MaybeTriggerNearbyGuardAlert(customer, severe: false);

            int withdrawalStage = UnderworldRuntime.GetWithdrawalStage(state);
            int payout = CalculatePayout(product, state, quantity);
            EClass.pc.ModCurrency(payout, "money2");
            ApplyAddictionProgress(state, potency);

            int now = EClass.world?.date?.GetRaw() ?? 0;
            bool curedWithdrawal = withdrawalStage > 0;
            UnderworldRuntime.MarkCustomerServed(state, now);
            state.PendingOrderQty = 0;
            state.PendingOrderPricePct = 100;
            state.PendingOrderMinPotency = 0;

            OverdoseResult overdose = MaybeTriggerOverdose(customer, state, product, potency, toxicity);
            if (overdose != OverdoseResult.Fatal)
            {
                state.Loyalty += overdose == OverdoseResult.Severe ? 2 : 1;
                if (curedWithdrawal)
                {
                    state.Loyalty += 3;
                    customer.RemoveCondition<ConUWWithdrawal>();
                }

                customer.ModAffinity(EClass.pc, Math.Max(1, 2 + state.Loyalty / 4), show: false);
            }

            EClass.pc.ModExp(291, 30);
            UnderworldRuntime.ModTerritoryRep(EClass._zone, UnderworldConfig.RepGainPerDeal.Value);
            state.LastOutcome = overdose == OverdoseResult.None
                ? $"{customer.Name} paid {payout} orens for a clean handoff."
                : state.LastOutcome;
            UnderworldRuntime.AddZoneHeat(EClass._zone, 1 + Math.Max(0, quantity / 2));
            UnderworldRuntime.SyncCustomerState(customer, state);

            if (overdose == OverdoseResult.Fatal)
            {
                return;
            }

            if (overdose == OverdoseResult.None)
            {
                Msg.SayRaw($"{customer.Name} palms the order and leaves {payout} orens behind.");
            }
        }

        private static void ShowCustomerStatus(Chara customer)
        {
            CustomerState state = UnderworldRuntime.GetCustomer(customer, create: false);
            if (state == null)
            {
                return;
            }

            GenerateStandingOrder(state);
            string preferred = ResolveCardName(state.PreferredProductId);
            string order = state.PendingOrderQty > 0
                ? $"{state.PendingOrderQty} x {preferred} (min potency {state.PendingOrderMinPotency})"
                : "No standing order";
            string cooldown = UnderworldRuntime.HasOfferCooldown(state)
                ? $"{UnderworldRuntime.GetOfferCooldownRemainingHours(state)}h"
                : "clear";
            string text = $"{customer.Name}\n"
                + $"Archetype: {UnderworldArchetypeService.GetLabel(state.LastKnownArchetype)}\n"
                + $"Loyalty: {FormatLoyaltyTier(state.Loyalty)} ({state.Loyalty})\n"
                + $"Addiction: {FormatAddictionTier(state.Addiction)} ({state.Addiction})\n"
                + $"Tolerance: {state.Tolerance}\n"
                + $"Withdrawal: {FormatWithdrawal(UnderworldRuntime.GetWithdrawalStage(state))}\n"
                + $"Pending order: {order}\n"
                + $"Offer cooldown: {cooldown}\n"
                + $"Last outcome: {state.LastOutcome}";

            Dialog.Ok(text);
        }

        private static bool GenerateStandingOrder(CustomerState state)
        {
            if (state == null || state.IsDead || state.Loyalty < 3 || state.PendingOrderQty > 0 || string.IsNullOrEmpty(state.PreferredProductId))
            {
                return false;
            }

            int now = EClass.world?.date?.GetRaw() ?? 0;
            if (state.LastOrderGeneratedRaw == now)
            {
                return false;
            }

            LoyaltyTier tier = GetLoyaltyTier(state.Loyalty);
            int quantity = 0;
            switch (tier)
            {
                case LoyaltyTier.Regular:
                    quantity = RandomRangeInclusive(1, 3);
                    break;
                case LoyaltyTier.Devoted:
                    quantity = RandomRangeInclusive(2, 5);
                    break;
                case LoyaltyTier.Hooked:
                    quantity = RandomRangeInclusive(3, 8);
                    break;
                // Note: Only Regulars+ (loyalty >= 3) reach this method.
                // Prospects are re-engaged via fresh sample offers in AppendChoices.
            }

            if (quantity <= 0)
            {
                return false;
            }

            quantity = Math.Max(1, (int)Math.Round(quantity * GetAddictionVolumeMultiplier(state.Addiction)));
            state.PendingOrderQty = quantity;
            state.PendingOrderPricePct = (int)Math.Round(GetLoyaltyPayMultiplier(tier) * 100f);
            state.PendingOrderMinPotency = Math.Max(10, state.Tolerance * 2);
            state.LastOrderGeneratedRaw = now;
            return true;
        }

        private static int ComputeAcceptanceChance(NpcArchetype archetype, bool guardNearby)
        {
            int chance = UnderworldConfig.SampleAcceptChanceBase.Value
                + EClass.pc.Evalue(291) * 2
                + UnderworldArchetypeService.GetAcceptModifier(archetype);
            if (guardNearby)
            {
                chance -= UnderworldConfig.GuardNearbyOfferPenalty.Value;
            }

            return Math.Max(5, Math.Min(95, chance));
        }

        private static int CalculatePayout(UnderworldProductDefinition product, CustomerState state, int quantity)
        {
            float loyaltyMult = state.PendingOrderPricePct / 100f;
            float addictionBonus = 1f + Math.Max(0, state.Addiction - 30) * UnderworldConfig.AddictionPriceBonusPerPoint.Value;
            float negotiationBonus = 1f + EClass.pc.Evalue(291) / 200f;
            float payout = product.BasePrice
                * UnderworldArchetypeService.GetPayMultiplier(state.LastKnownArchetype)
                * loyaltyMult
                * addictionBonus
                * negotiationBonus
                * (UnderworldConfig.DealingPayoutMultiplier.Value / 100f);
            return Math.Max(1, (int)Math.Round(payout)) * quantity;
        }

        private static void ApplyAddictionProgress(CustomerState state, int potency)
        {
            int addictionGain = Math.Max(1, (int)Math.Round(potency * UnderworldConfig.AddictionGainPerPotency.Value));
            state.Addiction = Math.Min(100, state.Addiction + addictionGain);
            if (potency > state.Tolerance * 2)
            {
                int toleranceGain = Math.Max(1, (int)Math.Round(potency * UnderworldConfig.ToleranceGainPerPotency.Value));
                state.Tolerance = Math.Min(50, state.Tolerance + toleranceGain);
            }
        }

        private static OverdoseResult MaybeTriggerOverdose(Chara customer, CustomerState state, UnderworldProductDefinition product, int potency, int toxicity)
        {
            if (state.Addiction < UnderworldConfig.ODAddictionThreshold.Value)
            {
                return OverdoseResult.None;
            }

            int potencyExcess = Math.Max(0, potency - state.Tolerance * 2);
            float addictionFactor = Math.Max(0f, (state.Addiction - 60) / 40f);
            float chance = UnderworldConfig.ODBaseChance.Value
                + potencyExcess * UnderworldConfig.ODPotencyFactor.Value
                + (toxicity / 100f) * UnderworldConfig.ODToxicityFactor.Value;
            chance *= 1f + addictionFactor;
            chance = Math.Min(chance, UnderworldConfig.ODMaxChance.Value);
            if (EClass.rnd(1000) >= chance * 1000f)
            {
                return OverdoseResult.None;
            }

            if (EClass.rnd(1000) < UnderworldConfig.ODFatalChance.Value * 1000f)
            {
                string summary = $"{customer.Name} died from an overdose. Customer relationship lost.";
                UnderworldRuntime.RecordFatalOverdose(customer, state, summary);
                UnderworldRuntime.AddZoneHeat(EClass._zone, UnderworldConfig.ODFatalHeatGain.Value);
                UnderworldRuntime.ModTerritoryRep(EClass._zone, UnderworldConfig.ODFatalRepPenalty.Value);
                EClass.player.ModKarma(UnderworldConfig.ODFatalKarmaPenalty.Value);
                CascadeCustomerFlight(state);
                ForceGuardCall(customer);
                Msg.SayRaw($"{customer.Name} drops hard and does not get back up.");
                customer.Die();
                return OverdoseResult.Fatal;
            }

            // Escalation: if customer already has an active OD, escalate severity
            int newStage;
            if (state.ActiveOverdoseStage >= 2)
            {
                // Already moderate or severe — escalate to severe (stage 3)
                newStage = 3;
            }
            else if (state.ActiveOverdoseStage == 1)
            {
                // Already mild — escalate to moderate (stage 2)
                newStage = 2;
            }
            else if (EClass.rnd(100) < 50)
            {
                // Fresh severe OD
                newStage = 3;
            }
            else
            {
                // Fresh mild OD
                newStage = 1;
            }

            int now = EClass.world?.date?.GetRaw() ?? 0;
            state.ActiveOverdoseStage = newStage;
            int durationMinutes = newStage switch
            {
                1 => 1440,   // 24h mild
                2 => 2880,   // 48h moderate
                _ => 4320,   // 72h severe
            };
            state.OverdoseExpiresRaw = now + durationMinutes;

            if (newStage >= 3)
            {
                state.LastOutcome = $"{customer.Name} suffered a severe overdose.";
                UnderworldRuntime.AddZoneHeat(EClass._zone, UnderworldConfig.ODSevereHeatGain.Value);
                if (EClass.rnd(100) < UnderworldConfig.ODSevereGuardAlertChance.Value)
                {
                    ForceGuardCall(customer);
                }

                Msg.SayRaw($"{customer.Name} collapses in the alley. Someone is going to notice.");
            }
            else if (newStage == 2)
            {
                state.LastOutcome = $"{customer.Name} is deteriorating from repeated use.";
                Msg.SayRaw($"{customer.Name} doubles over, worse than before.");
            }
            else
            {
                state.LastOutcome = $"{customer.Name} barely held together after a bad reaction.";
                Msg.SayRaw($"{customer.Name} staggers and waves off your help, but they still come back to you.");
            }

            UnderworldRuntime.SyncOverdoseCondition(customer, state);
            return newStage >= 3 ? OverdoseResult.Severe : OverdoseResult.Mild;
        }

        private static void CascadeCustomerFlight(CustomerState deadCustomer)
        {
            foreach (CustomerState state in UnderworldRuntime.ListCustomers())
            {
                if (state == deadCustomer || state.IsDead || !string.Equals(state.ZoneId, deadCustomer.ZoneId, StringComparison.Ordinal))
                {
                    continue;
                }

                float chance = UnderworldConfig.CustomerFlightChance.Value;
                if (GetLoyaltyTier(state.Loyalty) >= LoyaltyTier.Devoted)
                {
                    chance *= 0.5f;
                }

                if (state.Addiction >= 61)
                {
                    chance *= 0.1f;
                }

                if (EClass.rnd(1000) < chance * 1000f)
                {
                    state.Loyalty = 0;
                    state.Addiction = 0;
                    state.Tolerance = 0;
                    state.PendingOrderQty = 0;
                    state.PendingOrderMinPotency = 0;
                    state.PendingOrderPricePct = 100;
                    state.LastOutcome = $"{state.DisplayName} went cold after word spread about a fatal overdose.";
                }
            }
        }

        private static bool GuardNearby()
        {
            return EClass._map?.charas != null
                && EClass._map.charas.Any(c => c != null && c.trait is TraitGuard && c.Dist(EClass.pc) <= UnderworldConfig.GuardDetectionRadius.Value);
        }

        private static void MaybeTriggerRefusalGuardCall(Chara customer, NpcArchetype archetype)
        {
            if ((archetype == NpcArchetype.Noble || archetype == NpcArchetype.Scholar) && EClass.rnd(100) < 25)
            {
                EClass.player.ModKarma(-2);
                ForceGuardCall(customer);
            }
        }

        private static void MaybeTriggerNearbyGuardAlert(Chara customer, bool severe)
        {
            if (!GuardNearby())
            {
                return;
            }

            int chance = severe
                ? UnderworldConfig.ODSevereGuardAlertChance.Value
                : Math.Max(0, UnderworldConfig.NearbyGuardDealDetectionBase.Value - EClass.pc.Evalue(152) * (int)UnderworldConfig.NearbyGuardDealStealthReduction.Value);
            if (EClass.rnd(100) < chance)
            {
                ForceGuardCall(customer);
            }
        }

        private static void ForceGuardCall(Chara customer)
        {
            if (customer?.pos == null)
            {
                return;
            }

            customer.pos.CallGuard(EClass.pc, customer);
        }

        private static void ShowFixerBriefing()
        {
            Dialog.Ok(
                "The fixer keeps it simple.\n"
                + "Keep product concealed, do not work guards, and do not let desperate clients spiral without an antidote.\n\n"
                + $"Current nerve: {UnderworldRuntime.SyncNerve()}/{UnderworldRuntime.Data.MaxNerve}\n"
                + $"Local heat: {UnderworldRuntime.GetZoneHeat(EClass._zone)}\n"
                + "Prospects become regulars. Neglect turns into withdrawal. Bad batches turn into bodies."
            );
        }

        private static void OpenRestockDialog()
        {
            Dialog.Choice("Choose a quiet restock.", dialog =>
            {
                foreach (RestockOffer offer in RestockOffers)
                {
                    RestockOffer chosen = offer;
                    string label = $"{offer.Label} x{offer.Quantity} ({offer.Price} orens)";
                    dialog.AddButton(label, () => BuyRestock(chosen), close: true);
                }

                dialog.AddButton(Lang.Get("close"));
            });
        }

        private static void BuyRestock(RestockOffer offer)
        {
            if (EClass.pc.GetCurrency("money2") < offer.Price)
            {
                Msg.SayRaw("You do not have enough orens for that restock.");
                return;
            }

            EClass.pc.ModCurrency(-offer.Price, "money2");
            EClass.pc.AddThing(ThingGen.Create(offer.ItemId).SetNum(offer.Quantity));
            Msg.SayRaw($"The fixer slips you {offer.Quantity} {ResolveCardName(offer.ItemId)}.");
        }

        private static List<UnderworldProductDefinition> GetAvailableProducts(int minimum)
        {
            return UnderworldDrugCatalog.GetSampleableProducts()
                .Where(product => CountAccessibleProduct(product.ItemId) >= minimum)
                .OrderBy(product => product.BasePrice)
                .ToList();
        }

        private static int CountAccessibleProduct(string itemId)
        {
            return EClass.pc?.things.List(t => t.id == itemId, onlyAccessible: true).Sum(t => t.Num) ?? 0;
        }

        private static bool TryConsumeProduct(string itemId, int quantity, int minPotency, out int potency, out int toxicity)
        {
            potency = 0;
            toxicity = 0;
            List<Thing> candidates = EClass.pc.things.List(t => t.id == itemId, onlyAccessible: true)
                .OrderByDescending(UnderworldRuntime.GetProductPotency)
                .ToList();
            int available = candidates.Where(t => UnderworldRuntime.GetProductPotency(t) >= minPotency).Sum(t => t.Num);
            if (available < quantity)
            {
                return false;
            }

            int needed = quantity;
            int potencyTotal = 0;
            int toxicityTotal = 0;
            foreach (Thing thing in candidates)
            {
                int thingPotency = UnderworldRuntime.GetProductPotency(thing);
                if (thingPotency < minPotency)
                {
                    continue;
                }

                int take = Math.Min(needed, thing.Num);
                if (take <= 0)
                {
                    continue;
                }

                potencyTotal += thingPotency * take;
                toxicityTotal += UnderworldRuntime.GetProductToxicity(thing) * take;
                thing.ModNum(-take);
                needed -= take;
                if (needed <= 0)
                {
                    break;
                }
            }

            potency = Math.Max(1, potencyTotal / quantity);
            toxicity = Math.Max(0, toxicityTotal / quantity);
            return true;
        }

        private static CustomerState LookupDeadOrDormantCustomer(Chara customer)
        {
            return UnderworldRuntime.ListCustomers().FirstOrDefault(state => string.Equals(state.CustomerKey, customer.uid.ToString(), StringComparison.Ordinal));
        }

        private static LoyaltyTier GetLoyaltyTier(int loyalty)
        {
            if (loyalty >= 15)
            {
                return LoyaltyTier.Hooked;
            }

            if (loyalty >= 8)
            {
                return LoyaltyTier.Devoted;
            }

            if (loyalty >= 3)
            {
                return LoyaltyTier.Regular;
            }

            return LoyaltyTier.Prospect;
        }

        private static float GetLoyaltyPayMultiplier(LoyaltyTier tier)
        {
            switch (tier)
            {
                case LoyaltyTier.Regular:
                    return 1f;
                case LoyaltyTier.Devoted:
                    return 1.2f;
                case LoyaltyTier.Hooked:
                    return 1.3f;
                default:
                    return 0.8f;
            }
        }

        private static float GetAddictionVolumeMultiplier(int addiction)
        {
            if (addiction >= 86)
            {
                return 1.75f;
            }

            if (addiction >= 61)
            {
                return 1.5f;
            }

            if (addiction >= 31)
            {
                return 1.25f;
            }

            if (addiction >= 11)
            {
                return 1.1f;
            }

            return 1f;
        }

        private static int RandomRangeInclusive(int min, int max)
        {
            return min + EClass.rnd(max - min + 1);
        }

        private static string ResolveCardName(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return "Unknown";
            }

            CardRow row = EClass.sources.cards.map.TryGetValue(id);
            return row == null ? id : row.GetName();
        }

        private static string FormatLoyaltyTier(int loyalty)
        {
            switch (GetLoyaltyTier(loyalty))
            {
                case LoyaltyTier.Regular:
                    return "regular";
                case LoyaltyTier.Devoted:
                    return "devoted";
                case LoyaltyTier.Hooked:
                    return "hooked";
                default:
                    return "prospect";
            }
        }

        private static string FormatAddictionTier(int addiction)
        {
            if (addiction >= 86)
            {
                return "severe";
            }

            if (addiction >= 61)
            {
                return "addicted";
            }

            if (addiction >= 31)
            {
                return "dependent";
            }

            if (addiction >= 11)
            {
                return "casual";
            }

            return "clean";
        }

        private static string FormatWithdrawal(int stage)
        {
            switch (stage)
            {
                case 1:
                    return "restless";
                case 2:
                    return "shaking";
                case 3:
                    return "critical";
                default:
                    return "steady";
            }
        }

        private enum OverdoseResult
        {
            None,
            Mild,
            Severe,
            Fatal,
        }
    }
}

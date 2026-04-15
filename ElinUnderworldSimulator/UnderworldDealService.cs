using System;
using System.Collections.Generic;
using System.Linq;

namespace ElinUnderworldSimulator
{
    internal static class UnderworldDealService
    {
        private sealed class ProductDef
        {
            public string ItemId;
            public string Name;
            public int BasePrice;
            public int Potency;
            public int AddictionDelta;
        }

        private sealed class RestockOffer
        {
            public string ItemId;
            public string Label;
            public int Quantity;
            public int Price;
        }

        private static readonly ProductDef[] Products =
        {
            new ProductDef { ItemId = "uw_whisper_tonic", Name = "Whisper Tonic", BasePrice = 42, Potency = 1, AddictionDelta = 1 },
            new ProductDef { ItemId = "uw_dream_powder", Name = "Dream Powder", BasePrice = 78, Potency = 2, AddictionDelta = 2 },
            new ProductDef { ItemId = "uw_shadow_elixir", Name = "Shadow Elixir", BasePrice = 125, Potency = 3, AddictionDelta = 3 },
        };

        private static readonly RestockOffer[] RestockOffers =
        {
            new RestockOffer { ItemId = "uw_whispervine", Label = "Whispervine bundle", Quantity = 4, Price = 14 },
            new RestockOffer { ItemId = "uw_dreamblossom", Label = "Dreamblossom wrap", Quantity = 4, Price = 18 },
            new RestockOffer { ItemId = "uw_shadowcap", Label = "Shadowcap satchel", Quantity = 3, Price = 22 },
            new RestockOffer { ItemId = "uw_crude_moonite", Label = "Crude moonite pouch", Quantity = 2, Price = 26 },
            new RestockOffer { ItemId = "potion_empty", Label = "Empty bottle crate", Quantity = 4, Price = 10 },
            new RestockOffer { ItemId = ModInfo.AntidoteId, Label = "Antidote vial", Quantity = 1, Price = 35 },
        };

        internal static void AppendChoices(DramaCustomSequence sequence, Chara customer)
        {
            if (customer == null)
            {
                return;
            }

            if (customer.id == ModInfo.FixerId)
            {
                AppendFixerChoices(sequence);
                return;
            }

            if (!CanApproach(customer))
            {
                return;
            }

            UnderworldRuntime.SyncNerve();
            CustomerState state = UnderworldRuntime.GetCustomer(customer, create: false);
            if (state != null && state.Loyalty >= 2)
            {
                EnsurePendingOrder(customer, state);
            }

            sequence.Choice("Offer a sample", () => OpenSampleDialog(customer));

            if (state == null)
            {
                return;
            }

            if (state.PendingOrderQty > 0)
            {
                sequence.Choice("Fulfill their standing order", () => FulfillPendingOrder(customer));
            }
            else if (!string.IsNullOrEmpty(state.PreferredProductId))
            {
                sequence.Choice("Talk shop", () => ShowCustomerStatus(customer));
            }
        }

        internal static void ShowLedger()
        {
            DealerLedgerDialog.Open();
        }

        internal static bool TryUseAntidoteOn(Card target)
        {
            Chara customer = target as Chara;
            if (customer == null)
            {
                Msg.SayRaw("The antidote needs a living target.");
                return false;
            }

            CustomerState state = UnderworldRuntime.GetCustomer(customer, create: false);
            if (state == null || state.ActiveOverdoseStage < 2)
            {
                Msg.SayRaw($"{customer.Name} does not need an antidote right now.");
                return false;
            }

            state.ActiveOverdoseStage = 0;
            state.LastOutcome = "Stabilized with an antidote.";
            customer.RemoveCondition<ConFaint>();
            customer.RemoveCondition<ConDrunk>();
            customer.ModAffinity(EClass.pc, 2, show: false);
            Msg.SayRaw($"{customer.Name} steadies after the antidote takes hold.");
            return true;
        }

        private static void AppendFixerChoices(DramaCustomSequence sequence)
        {
            sequence.Choice("Ask for the ground rules", ShowFixerBriefing);
            sequence.Choice("Buy a quiet restock", OpenRestockDialog);
            sequence.Choice("Review the local ledger", ShowLedger);
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

            return !EClass._zone.IsRegion;
        }

        private static void OpenSampleDialog(Chara customer)
        {
            if (EClass.pc.things.Find(ModInfo.SampleKitId) == null)
            {
                Msg.SayRaw("You need the sample kit before you can float product.");
                return;
            }

            if (!UnderworldRuntime.SpendNerve(1))
            {
                Msg.SayRaw("Your nerve is shot. Wait a while before trying again.");
                return;
            }

            List<ProductDef> available = GetAvailableProducts(minimum: 1);
            if (available.Count == 0)
            {
                Msg.SayRaw("You do not have anything ready to sample.");
                return;
            }

            Dialog.Choice("Choose a sample to float.", dialog =>
            {
                foreach (ProductDef product in available)
                {
                    ProductDef chosen = product;
                    Thing stack = EClass.pc.things.Find(product.ItemId);
                    string label = $"{product.Name} x{stack?.Num ?? 0}";
                    dialog.AddButton(label, () => OfferSample(customer, chosen), close: true);
                }

                dialog.AddButton(Lang.Get("close"));
            });
        }

        private static void OfferSample(Chara customer, ProductDef product)
        {
            Thing stack = EClass.pc.things.Find(product.ItemId);
            if (stack == null || stack.Num <= 0)
            {
                Msg.SayRaw("You ran out before the offer landed.");
                return;
            }

            stack.ModNum(-1);

            CustomerState state = UnderworldRuntime.GetCustomer(customer, create: true);
            int heat = UnderworldRuntime.GetZoneHeat(EClass._zone);
            int affinityStage = (int)customer.affinity.CurrentStage;
            int chance = 25 + customer.interest / 4 + affinityStage * 4 + state.Loyalty * 6;
            chance -= heat * 5;
            chance = Math.Max(10, Math.Min(90, chance));

            bool accepted = EClass.rnd(100) < chance;
            if (!accepted)
            {
                state.LastOutcome = $"Turned down a sample of {product.Name}.";
                customer.ModAffinity(EClass.pc, -2 - EClass.rnd(3), show: false);
                UnderworldRuntime.AddZoneHeat(EClass._zone, 1);
                TryTriggerWitness(customer, severity: 1);
                Msg.SayRaw($"{customer.Name} reads the room, then waves the offer away.");
                return;
            }

            state.PreferredProductId = product.ItemId;
            state.Loyalty = Math.Max(state.Loyalty, 1);
            state.Addiction += product.AddictionDelta;
            state.LastSampleRaw = EClass.world.date.GetRaw();
            state.LastOutcome = $"Accepted a sample of {product.Name}.";
            customer.ModAffinity(EClass.pc, 2 + EClass.rnd(2), show: false);

            EnsurePendingOrder(customer, state, force: true);
            Msg.SayRaw($"{customer.Name} takes the sample and asks if you can find more later.");
        }

        private static void FulfillPendingOrder(Chara customer)
        {
            CustomerState state = UnderworldRuntime.GetCustomer(customer, create: false);
            if (state == null)
            {
                Msg.SayRaw("They do not know you in that way yet.");
                return;
            }

            EnsurePendingOrder(customer, state);
            if (state.PendingOrderQty <= 0)
            {
                Msg.SayRaw($"{customer.Name} is keeping their distance for now.");
                return;
            }

            ProductDef product = GetProduct(state.PreferredProductId);
            if (product == null)
            {
                Msg.SayRaw("Their usual order no longer makes sense.");
                return;
            }

            Thing stack = EClass.pc.things.Find(product.ItemId);
            if (stack == null || stack.Num < state.PendingOrderQty)
            {
                Msg.SayRaw($"You need {state.PendingOrderQty} {product.Name} to close this order.");
                return;
            }

            if (!UnderworldRuntime.SpendNerve(1))
            {
                Msg.SayRaw("You need time to steady yourself before making another handoff.");
                return;
            }

            int withdrawalStage = UnderworldRuntime.GetWithdrawalStage(state);
            int qty = state.PendingOrderQty;
            int price = product.BasePrice * qty * Math.Max(60, state.PendingOrderPricePct + state.Loyalty * 5 - state.Tolerance * 4) / 100;

            stack.ModNum(-qty);
            EClass.pc.ModCurrency(price, "money2");

            state.Loyalty += 1;
            state.Addiction += product.AddictionDelta + 1;
            state.Tolerance = Math.Min(8, state.Tolerance + 1);
            state.LastServedRaw = EClass.world.date.GetRaw();
            state.PendingOrderQty = 0;
            state.PendingOrderPricePct = 100;
            state.PendingOrderMinPotency = 0;
            state.LastOutcome = $"Order closed for {price} orens.";

            customer.ModAffinity(EClass.pc, Math.Max(1, 2 + state.Loyalty / 2), show: false);

            int heatGain = 1 + qty / 2 + withdrawalStage;
            UnderworldRuntime.AddZoneHeat(EClass._zone, heatGain);
            MaybeTriggerOverdose(customer, state, product, withdrawalStage);
            TryTriggerWitness(customer, severity: heatGain);

            Msg.SayRaw($"{customer.Name} palms the order and leaves {price} orens behind.");
        }

        private static void ShowCustomerStatus(Chara customer)
        {
            CustomerState state = UnderworldRuntime.GetCustomer(customer, create: false);
            if (state == null)
            {
                return;
            }

            EnsurePendingOrder(customer, state);

            string preferred = ResolveCardName(state.PreferredProductId);
            string order = state.PendingOrderQty > 0
                ? $"{state.PendingOrderQty} x {preferred}"
                : "No standing order yet.";
            string text = $"{customer.Name}\n"
                + $"Preferred: {preferred}\n"
                + $"Loyalty: {state.Loyalty}\n"
                + $"Addiction: {state.Addiction}\n"
                + $"Tolerance: {state.Tolerance}\n"
                + $"Withdrawal: {FormatWithdrawal(UnderworldRuntime.GetWithdrawalStage(state))}\n"
                + $"Pending order: {order}\n"
                + $"Last outcome: {state.LastOutcome ?? "None"}";

            Dialog.Ok(text);
        }

        private static void EnsurePendingOrder(Chara customer, CustomerState state, bool force = false)
        {
            if (state == null || string.IsNullOrEmpty(state.PreferredProductId))
            {
                return;
            }

            if (!force)
            {
                if (state.PendingOrderQty > 0 || state.Loyalty < 2)
                {
                    return;
                }

                int now = EClass.world.date.GetRaw();
                if (state.LastOrderGeneratedRaw > 0 && now - state.LastOrderGeneratedRaw < 960)
                {
                    return;
                }
            }

            int withdrawal = UnderworldRuntime.GetWithdrawalStage(state);
            int quantity = force ? 1 : Math.Min(3, 1 + state.Loyalty / 2 + EClass.rnd(2));
            state.PendingOrderQty = Math.Max(1, quantity + Math.Max(0, withdrawal - 1));
            state.PendingOrderPricePct = 100 + state.Loyalty * 4 + withdrawal * 8;
            state.PendingOrderMinPotency = Math.Max(1, GetProduct(state.PreferredProductId)?.Potency ?? 1);
            state.LastOrderGeneratedRaw = EClass.world.date.GetRaw();
        }

        private static void MaybeTriggerOverdose(Chara customer, CustomerState state, ProductDef product, int withdrawalStage)
        {
            int risk = product.Potency * 12 + state.Addiction * 8 - state.Tolerance * 5 + withdrawalStage * 10;
            risk += UnderworldRuntime.GetZoneHeat(EClass._zone) * 3;
            risk = Math.Max(0, Math.Min(85, risk));

            if (EClass.rnd(100) >= risk)
            {
                state.ActiveOverdoseStage = 0;
                return;
            }

            int roll = EClass.rnd(100);
            if (roll < 15 + product.Potency * 5)
            {
                state.ActiveOverdoseStage = 3;
                state.LastOutcome = $"{customer.Name} overdosed and died.";
                UnderworldRuntime.AddZoneHeat(EClass._zone, 4);
                EClass.player.ModKarma(-5);
                Msg.SayRaw($"{customer.Name} drops hard and does not get back up.");
                customer.Die();
                return;
            }

            if (roll < 60)
            {
                state.ActiveOverdoseStage = 2;
                state.LastOutcome = $"{customer.Name} went into a bad overdose.";
                customer.AddCondition<ConFaint>(200, force: true);
                customer.AddCondition<ConDrunk>(200, force: true);
                UnderworldRuntime.AddZoneHeat(EClass._zone, 2);
                Msg.SayRaw($"{customer.Name} slumps against the wall, breathing shallow.");
                return;
            }

            state.ActiveOverdoseStage = 1;
            state.LastOutcome = $"{customer.Name} barely kept it together.";
            customer.AddCondition<ConDrunk>(120, force: true);
            customer.ModAffinity(EClass.pc, -2, show: false);
            UnderworldRuntime.AddZoneHeat(EClass._zone, 1);
            Msg.SayRaw($"{customer.Name} sways, then steadies with a grimace.");
        }

        private static void TryTriggerWitness(Chara customer, int severity)
        {
            int heat = UnderworldRuntime.GetZoneHeat(EClass._zone);
            bool witnessed = EClass.pc.pos.TryWitnessCrime(
                EClass.pc,
                customer,
                5,
                witness => EClass.rnd(100) < Math.Min(90, 10 + severity * 10 + heat * 8));

            if (!witnessed)
            {
                return;
            }

            EClass.player.ModKarma(-Math.Max(1, severity));
            Msg.SayRaw("The handoff goes hot. Someone saw enough to make trouble.");
        }

        private static void ShowFixerBriefing()
        {
            int heat = UnderworldRuntime.GetZoneHeat(EClass._zone);
            Dialog.Ok(
                "The fixer speaks in a low voice.\n"
                + "Keep your nerve up, do not work the guards, and never let a shaky client spiral without an antidote.\n\n"
                + $"Current nerve: {UnderworldRuntime.SyncNerve()}/{UnderworldRuntime.Data.MaxNerve}\n"
                + $"Local heat: {heat}\n"
                + "Samples create prospects. Reliable handoffs create regulars. Neglect breeds withdrawal."
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
            EClass.player.DropReward(ThingGen.Create(offer.ItemId).SetNum(offer.Quantity));
            Msg.SayRaw($"The fixer slips you {offer.Quantity} {ResolveCardName(offer.ItemId)}.");
        }

        private static List<ProductDef> GetAvailableProducts(int minimum)
        {
            List<ProductDef> list = new List<ProductDef>();
            foreach (ProductDef product in Products)
            {
                Thing thing = EClass.pc.things.Find(product.ItemId);
                if (thing != null && thing.Num >= minimum)
                {
                    list.Add(product);
                }
            }

            return list;
        }

        private static ProductDef GetProduct(string id)
        {
            return Products.FirstOrDefault(product => product.ItemId == id);
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

        private static string FormatWithdrawal(int stage)
        {
            switch (stage)
            {
                case 0:
                    return "Clear";
                case 1:
                    return "Restless";
                case 2:
                    return "Shaking";
                default:
                    return "Critical";
            }
        }
    }
}

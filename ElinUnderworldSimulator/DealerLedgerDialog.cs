using System;
using System.Linq;

namespace ElinUnderworldSimulator
{
    internal sealed class DealerLedgerDialog
    {
        private Dialog dialog;

        internal static void Open()
        {
            new DealerLedgerDialog().Show();
        }

        private void Show()
        {
            dialog = Layer.Create<Dialog>();
            if (dialog.windows.Count > 0)
            {
                dialog.windows[0].SetCaption("Dealer's Ledger");
            }

            dialog.textDetail.SetText("");
            dialog.spacer.SetActive(enable: false);
            Rebuild();
            dialog.AddButton(Lang.Get("close"));
            ELayer.ui.AddLayer(dialog);
        }

        private void Rebuild()
        {
            UINote note = dialog.note;
            note.Clear();
            note.AddHeader("HeaderNote", "Dealer's Ledger");
            note.AddTopic("TopicLeft", "Nerve", $"{UnderworldRuntime.SyncNerve()}/{UnderworldRuntime.Data.MaxNerve}");
            note.AddTopic("TopicLeft", "Local Heat", UnderworldRuntime.GetZoneHeat(EClass._zone).ToString());
            note.AddTopic("TopicLeft", "Territory Rep", UnderworldRuntime.GetTerritoryRep(EClass._zone).ToString());
            note.Space();

            var customers = UnderworldRuntime.ListCustomers()
                .OrderBy(state => state.ZoneName)
                .ThenByDescending(state => state.Loyalty)
                .ThenBy(state => state.DisplayName)
                .ToList();

            if (customers.Count == 0)
            {
                note.AddText("No names in the ledger yet. Float a sample and start building a route.");
                note.Build();
                return;
            }

            foreach (var zoneGroup in customers.GroupBy(state => state.ZoneName))
            {
                note.AddHeader("HeaderNote", zoneGroup.Key ?? "Unknown");
                foreach (CustomerState state in zoneGroup)
                {
                    string status = BuildStatus(state);
                    string wants = state.PendingOrderQty > 0
                        ? $"{state.PendingOrderQty}x {ResolveCardName(state.PreferredProductId)}"
                        : "No order";
                    note.AddTopic("TopicLeft", BuildRowLabel(state), $"{status} | {wants}");

                    string alert = BuildAlert(state);
                    if (!string.IsNullOrEmpty(alert))
                    {
                        note.AddText(alert, state.IsDead ? FontColor.Bad : FontColor.Warning);
                    }
                }

                note.Space();
            }

            note.Build();
        }

        private static string BuildRowLabel(CustomerState state)
        {
            string archetype = UnderworldArchetypeService.GetLabel(state.LastKnownArchetype);
            return $"{state.DisplayName ?? "Unknown"} ({archetype})";
        }

        private static string BuildStatus(CustomerState state)
        {
            if (state.IsDead)
            {
                return "deceased";
            }

            string loyalty = state.Loyalty >= 15 ? "hooked"
                : state.Loyalty >= 8 ? "devoted"
                : state.Loyalty >= 3 ? "regular"
                : "prospect";
            string addiction = state.Addiction >= 86 ? "severe"
                : state.Addiction >= 61 ? "addicted"
                : state.Addiction >= 31 ? "dependent"
                : state.Addiction >= 11 ? "casual"
                : "clean";
            string withdrawal = UnderworldRuntime.GetWithdrawalStage(state) switch
            {
                1 => "restless",
                2 => "shaking",
                3 => "critical",
                _ => "steady",
            };
            return $"{loyalty} | {addiction} | withdrawal {withdrawal}";
        }

        private static string BuildAlert(CustomerState state)
        {
            if (state.IsDead)
            {
                return state.DeathSummary;
            }

            if (state.ActiveOverdoseStage >= 2)
            {
                return $"{state.DisplayName} is in severe overdose danger.";
            }

            if (state.Addiction >= 86)
            {
                return $"{state.DisplayName} is showing signs of severe dependency.";
            }

            if (UnderworldRuntime.HasOfferCooldown(state))
            {
                return $"{state.DisplayName} is cooling off for another {UnderworldRuntime.GetOfferCooldownRemainingHours(state)}h.";
            }

            return state.LastOutcome;
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
    }
}

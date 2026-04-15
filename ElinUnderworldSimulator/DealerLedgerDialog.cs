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
            note.Space();

            var customers = UnderworldRuntime.ListCustomers()
                .OrderByDescending(state => state.Loyalty)
                .ThenBy(state => state.DisplayName)
                .ToList();

            if (customers.Count == 0)
            {
                note.AddText("No names in the ledger yet. Float a sample and start building a list.");
                note.Build();
                return;
            }

            foreach (CustomerState state in customers)
            {
                string preferred = string.IsNullOrEmpty(state.PreferredProductId)
                    ? "Uncommitted"
                    : EClass.sources.cards.map.TryGetValue(state.PreferredProductId)?.GetName() ?? state.PreferredProductId;
                string order = state.PendingOrderQty > 0
                    ? $"{state.PendingOrderQty} ready"
                    : "No order";
                string status = $"{preferred} | L{state.Loyalty} A{state.Addiction} T{state.Tolerance} | {order} | {FormatWithdrawal(UnderworldRuntime.GetWithdrawalStage(state))}";

                note.AddTopic("TopicLeft", state.DisplayName ?? "Unknown", status);
                if (!string.IsNullOrEmpty(state.LastOutcome))
                {
                    note.AddText(state.LastOutcome, FontColor.Topic);
                }
            }

            note.Build();
        }

        private static string FormatWithdrawal(int stage)
        {
            switch (stage)
            {
                case 0:
                    return "steady";
                case 1:
                    return "restless";
                case 2:
                    return "shaking";
                default:
                    return "critical";
            }
        }
    }
}

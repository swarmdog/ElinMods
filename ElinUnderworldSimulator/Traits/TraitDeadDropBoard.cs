using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ElinUnderworldSimulator;

public class TraitDeadDropBoard : TraitItem
{
    public override string LangUse => "Check Dead Drop";

    public override bool CanUse(Chara c) => true;

    public override bool OnUse(Chara c)
    {
        UnderworldDeadDropDialog.Open();
        return true;
    }
}

namespace ElinUnderworldSimulator
{
    internal sealed class UnderworldDeadDropDialog
    {
        private Dialog dialog;

        public static void Open()
        {
            new UnderworldDeadDropDialog().Show();
        }

        private void Show()
        {
            dialog = Layer.Create<Dialog>();
            if (dialog.windows.Count > 0)
                dialog.windows[0].SetCaption("Dead Drop Board");

            dialog.textDetail.SetText("");
            dialog.spacer.SetActive(enable: false);
            RebuildNote();
            dialog.AddButton("Refresh", Refresh, close: false);
            dialog.AddButton(Lang.Get("close"));
            ELayer.ui.AddLayer(dialog);

            if (UnderworldPlugin.IsOnlineReady())
            {
                System.Threading.Tasks.Task.Run(async () =>
                {
                    try
                    {
                        await UnderworldPlugin.NetworkClient.GetAvailableOrdersAsync();
                    }
                    catch { }
                }).ContinueWith(_ => RebuildNote(), TaskScheduler.FromCurrentSynchronizationContext());
            }
        }

        private void Refresh()
        {
            if (!UnderworldPlugin.IsOnlineReady()) return;
            System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    await UnderworldPlugin.NetworkClient.GetAvailableOrdersAsync();
                }
                catch { }
            }).ContinueWith(_ => RebuildNote(), TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void RebuildNote()
        {
            if (dialog == null || dialog.note == null) return;
            UINote note = dialog.note;
            note.Clear();
            note.AddHeader("HeaderNote", "Available Contracts");

            if (!UnderworldPlugin.IsOnlineReady())
            {
                note.AddText("The board is empty. No network connection.", FontColor.Warning);
                note.Build();
                return;
            }

            List<UnderworldOrderDto> orders = UnderworldPlugin.NetworkState?.AvailableOrders;
            if (orders == null || orders.Count == 0)
            {
                note.AddText("No contracts available at the moment.", FontColor.Default);
                note.Build();
                return;
            }

            note.AddText("Active contracts from the underworld network. Accept through your Dealer Ledger.", FontColor.FoodQuality);
            note.Space();

            foreach (UnderworldOrderDto order in orders)
            {
                string clientTag = order.ClientName.TagColor(FontColor.Topic);
                note.AddTopic("TopicLeft", order.TerritoryName, clientTag);
                note.AddText($"  Product: {order.ProductType} | Qty: {order.MinQuantity}-{order.MaxQuantity} | Payout: {order.BasePayout:N0}g");
                note.AddText($"  Min potency: {order.MinPotency} | Max tox: {order.MaxToxicity} | Deadline: {order.DeadlineHours}h");
            }

            note.Space();
            note.AddText($"Showing {orders.Count} contract(s).", FontColor.FoodQuality);
            note.Build();
        }
    }
}

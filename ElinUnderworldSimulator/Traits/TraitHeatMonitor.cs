using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ElinUnderworldSimulator;

public class TraitHeatMonitor : TraitItem
{
    public override string LangUse => "Check Heat Levels";

    public override bool CanUse(Chara c) => true;

    public override bool OnUse(Chara c)
    {
        UnderworldHeatMonitorDialog.Open();
        return true;
    }
}

namespace ElinUnderworldSimulator
{
    internal sealed class UnderworldHeatMonitorDialog
    {
        private Dialog dialog;

        public static void Open()
        {
            new UnderworldHeatMonitorDialog().Show();
        }

        private void Show()
        {
            dialog = Layer.Create<Dialog>();
            if (dialog.windows.Count > 0)
                dialog.windows[0].SetCaption("Heat Monitor");

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
                        await UnderworldPlugin.NetworkClient.GetTerritoriesAsync();
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
                    await UnderworldPlugin.NetworkClient.GetTerritoriesAsync();
                }
                catch { }
            }).ContinueWith(_ => RebuildNote(), TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void RebuildNote()
        {
            if (dialog == null || dialog.note == null) return;
            UINote note = dialog.note;
            note.Clear();
            note.AddHeader("HeaderNote", "Enforcement Heat Status");

            if (!UnderworldPlugin.IsOnlineReady())
            {
                note.AddText("The monitor is dark. No network connection.", FontColor.Warning);
                note.Build();
                return;
            }

            List<UnderworldTerritoryDto> territories = UnderworldPlugin.NetworkState?.Territories;
            if (territories == null || territories.Count == 0)
            {
                note.AddText("Calibrating heat sensors...", FontColor.Warning);
                note.Build();
                return;
            }

            note.AddText("Real-time enforcement heat across all territories. Higher heat increases inspection and bust risk.", FontColor.FoodQuality);
            note.Space();

            foreach (UnderworldTerritoryDto territory in territories)
            {
                FontColor heatColor = GetHeatColor(territory.HeatLevel);
                string levelTag = territory.HeatLevel.ToUpper().TagColor(heatColor);
                string bar = BuildHeatBar(territory.Heat, 100);
                note.AddTopic("TopicLeft", territory.Name, levelTag);
                note.AddText($"  {bar}  ({territory.Heat}/100)");
            }

            note.Space();
            note.AddText("Heat decays over time but spikes with each shipment. Lockdown territories trigger guaranteed inspections.", FontColor.FoodQuality);
            note.Build();
        }

        private static string BuildHeatBar(int heat, int capacity)
        {
            int filled = Math.Max(0, Math.Min(20, (int)Math.Round(20.0 * heat / Math.Max(1, capacity))));
            return new string('█', filled) + new string('░', 20 - filled);
        }

        private static FontColor GetHeatColor(string level)
        {
            switch (level)
            {
                case "clear": return FontColor.Good;
                case "elevated": return FontColor.Default;
                case "high": return FontColor.Warning;
                case "critical": return FontColor.Bad;
                case "lockdown": return FontColor.Bad;
                default: return FontColor.Default;
            }
        }
    }
}

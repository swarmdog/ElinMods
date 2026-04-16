using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ElinUnderworldSimulator;

public class TraitTerritoryMap : TraitItem
{
    public override string LangUse => "View Territory Map";

    public override bool CanUse(Chara c) => true;

    public override bool OnUse(Chara c)
    {
        UnderworldTerritoryMapDialog.Open();
        return true;
    }
}

namespace ElinUnderworldSimulator
{
    internal sealed class UnderworldTerritoryMapDialog
    {
        private Dialog dialog;

        public static void Open()
        {
            new UnderworldTerritoryMapDialog().Show();
        }

        private void Show()
        {
            dialog = Layer.Create<Dialog>();
            if (dialog.windows.Count > 0)
                dialog.windows[0].SetCaption("Underworld Territory Map");

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
            note.AddHeader("HeaderNote", "Territory Intelligence");

            if (!UnderworldPlugin.IsOnlineReady())
            {
                note.AddText("The map is inactive. Connect to the Underworld network to view territory data.", FontColor.Warning);
                note.Build();
                return;
            }

            List<UnderworldTerritoryDto> territories = UnderworldPlugin.NetworkState?.Territories;
            if (territories == null || territories.Count == 0)
            {
                note.AddText("Compiling territory reports...", FontColor.Warning);
                note.Build();
                return;
            }

            note.AddText("Active territories across the underworld network. Heat determines enforcement risk.", FontColor.FoodQuality);
            note.Space();

            foreach (UnderworldTerritoryDto territory in territories)
            {
                FontColor heatColor = GetHeatColor(territory.HeatLevel);
                string heatTag = territory.HeatLevel.TagColor(heatColor);
                string controller = territory.ControllingFaction ?? "Contested";
                note.AddTopic("TopicLeft", territory.Name, heatTag);
                note.AddText($"  Heat: {territory.Heat} | Control: {controller} | Orders: {territory.AvailableOrdersCount}");
            }

            note.Space();
            note.AddText("Territory control is resolved daily. Factions accumulate influence through successful shipments.", FontColor.FoodQuality);
            note.Build();
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

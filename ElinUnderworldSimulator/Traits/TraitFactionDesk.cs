using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ElinUnderworldSimulator;

public class TraitFactionDesk : TraitItem
{
    public override string LangUse => "Manage Faction";

    public override bool CanUse(Chara c) => true;

    public override bool OnUse(Chara c)
    {
        UnderworldFactionDeskDialog.Open();
        return true;
    }
}

namespace ElinUnderworldSimulator
{
    internal sealed class UnderworldFactionDeskDialog
    {
        private Dialog dialog;

        public static void Open()
        {
            new UnderworldFactionDeskDialog().Show();
        }

        private void Show()
        {
            dialog = Layer.Create<Dialog>();
            if (dialog.windows.Count > 0)
                dialog.windows[0].SetCaption("Faction Operations");

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
                        await UnderworldPlugin.NetworkClient.GetPlayerStatusAsync();
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
                    await UnderworldPlugin.NetworkClient.GetPlayerStatusAsync();
                    UnderworldPlayerFactionDto faction = UnderworldPlugin.NetworkState?.PlayerStatus?.Faction;
                    if (faction != null)
                    {
                        await UnderworldPlugin.NetworkClient.GetFactionAsync(faction.Id);
                    }
                }
                catch { }
            }).ContinueWith(_ => RebuildNote(), TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void RebuildNote()
        {
            if (dialog == null || dialog.note == null) return;
            UINote note = dialog.note;
            note.Clear();
            note.AddHeader("HeaderNote", "Faction Intelligence");

            if (!UnderworldPlugin.IsOnlineReady())
            {
                note.AddText("The desk is silent. No network connection.", FontColor.Warning);
                note.Build();
                return;
            }

            UnderworldPlayerStatusDto status = UnderworldPlugin.NetworkState?.PlayerStatus;
            if (status == null)
            {
                note.AddText("Retrieving operative status...", FontColor.Warning);
                note.Build();
                return;
            }

            if (status.Faction == null)
            {
                note.AddText("You are not part of any faction.", FontColor.Default);
                note.Space();
                note.AddText("Create or join a faction to coordinate territory operations with other operatives.", FontColor.FoodQuality);
                note.Build();
                return;
            }

            note.AddTopic("TopicLeft", status.Faction.Name, status.Faction.Role.TagColor(FontColor.Topic));
            note.Space();
            note.AddText($"Your role: {status.Faction.Role}", FontColor.Default);
            note.AddText($"Rank: {status.RankName} (Tier {status.UnderworldRank})", FontColor.Default);
            note.Space();

            note.AddHeaderTopic("Operations Summary");
            note.AddText($"Active orders: {status.ActiveOrdersCount}");
            note.AddText($"Gold reserves: {status.Gold:N0}");

            if (status.ReputationByTerritory != null && status.ReputationByTerritory.Count > 0)
            {
                note.Space();
                note.AddHeaderTopic("Territory Reputation");
                foreach (KeyValuePair<string, int> entry in status.ReputationByTerritory)
                {
                    note.AddText($"  {entry.Key}: {entry.Value}");
                }
            }

            note.Space();
            note.AddText("Faction management actions are available through the Underworld network.", FontColor.FoodQuality);
            note.Build();
        }
    }
}

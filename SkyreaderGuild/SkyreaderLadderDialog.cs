using System;
using System.Linq;

namespace SkyreaderGuild
{
    internal sealed class SkyreaderLadderDialog
    {
        private Dialog dialog;

        public static void Open()
        {
            new SkyreaderLadderDialog().Show();
        }

        private void Show()
        {
            dialog = Layer.Create<Dialog>();
            if (dialog.windows.Count > 0)
            {
                dialog.windows[0].SetCaption("Starlight Ladder");
            }

            dialog.textDetail.SetText("");
            dialog.spacer.SetActive(enable: false);
            SkyreaderGuild.RefreshLadderPlaque(force: false, onUpdated: OnFetchComplete);
            RebuildNote();
            dialog.AddButton("Refresh", Refresh, close: false);
            dialog.AddButton(Lang.Get("close"));
            ELayer.ui.AddLayer(dialog);
        }

        private void Refresh()
        {
            SkyreaderGuild.RefreshLadderPlaque(force: true, onUpdated: OnFetchComplete);
            RebuildNote();
        }

        private void OnFetchComplete()
        {
            if (dialog == null || dialog.gameObject == null)
                return;

            RebuildNote();
        }

        private void RebuildNote()
        {
            if (dialog == null || dialog.note == null)
                return;

            SkyreaderLadderClient.LadderPlaqueView view = SkyreaderGuild.GetLadderPlaqueView();
            UINote note = dialog.note;
            note.Clear();
            note.AddHeader("HeaderNote", "Starlight Ladder");

            if (!view.IsOnlineReady)
            {
                note.AddText(SkyreaderGuild.GetLadderUnavailableText(), FontColor.Warning);
                note.Build();
                return;
            }

            if (!view.HasCache)
            {
                if (!view.Error.IsEmpty())
                {
                    note.AddText(view.Error, FontColor.Warning);
                }
                else
                {
                    note.AddText("The Starlight Ladder is aligning. Check the plaque again in a moment.");
                }

                if (view.IsFetching)
                {
                    note.Space();
                    note.AddText("The plaque is tracing fresh constellations.", FontColor.Topic);
                }

                note.Build();
                return;
            }

            note.AddHeaderTopic("Current");
            note.AddTopic("TopicLeft", "Rank", FormatRank(view.CurrentRank));
            note.AddTopic("TopicLeft", "Starlight", view.CurrentScore.ToFormat());
            if (view.TotalPlayers > 0)
            {
                note.AddTopic("TopicLeft", "Skyreaders", view.TotalPlayers.ToFormat());
            }

            if (view.Percentile.HasValue)
            {
                note.AddTopic("TopicLeft", "Percentile", $"{view.Percentile.Value:0.##}");
            }

            note.Space();
            note.AddHeaderTopic("Ranking");

            if (view.Entries == null || view.Entries.Count == 0)
            {
                note.AddText("No entries have reached the plaque yet.");
            }
            else
            {
                foreach (SkyreaderLadderClient.LadderPlaqueEntry entry in view.Entries.Take(20))
                {
                    string name = entry.DisplayName.IsEmpty("Unknown Skyreader");
                    string value = $"{name}     {entry.TotalScore.ToFormat()}";
                    if (entry.IsPlayer)
                    {
                        value = value.TagColor(FontColor.Good);
                    }

                    note.AddTopic("TopicLeft", $"#{entry.Rank}", value);
                }
            }

            if (view.IsFetching)
            {
                note.Space();
                note.AddText("The plaque is refreshing its star chart.", FontColor.Topic);
            }

            if (!view.Error.IsEmpty())
            {
                note.Space();
                note.AddText(view.Error, FontColor.Warning);
            }

            note.Build();
        }

        private static string FormatRank(int? rank)
        {
            return rank.HasValue ? $"#{rank.Value}" : "Unranked";
        }
    }
}

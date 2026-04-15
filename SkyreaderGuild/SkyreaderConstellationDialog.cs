using System;
using System.Collections.Generic;
using System.Linq;

namespace SkyreaderGuild
{
    internal sealed class SkyreaderConstellationDialog
    {
        private Dialog dialog;

        public static void Open()
        {
            new SkyreaderConstellationDialog().Show();
        }

        private void Show()
        {
            dialog = Layer.Create<Dialog>();
            if (dialog.windows.Count > 0)
                dialog.windows[0].SetCaption("Patron Constellations");

            dialog.textDetail.SetText("");
            dialog.spacer.SetActive(enable: false);
            SkyreaderGuild.RefreshConstellations(force: false, onDone: OnFetchComplete);
            RebuildNote();
            dialog.AddButton("Refresh", Refresh, close: false);
            dialog.AddButton(Lang.Get("close"));
            ELayer.ui.AddLayer(dialog);
        }

        private void Refresh()
        {
            SkyreaderGuild.RefreshConstellations(force: true, onDone: OnFetchComplete);
            RebuildNote();
        }

        private void OnFetchComplete()
        {
            if (dialog == null || dialog.gameObject == null) return;
            RebuildNote();
        }

        private void RebuildNote()
        {
            if (dialog == null || dialog.note == null) return;

            var data = SkyreaderGuild.OnlineClient?.GetCachedConstellations();
            UINote note = dialog.note;
            note.Clear();
            note.AddHeader("HeaderNote", "Patron Constellations");

            if (!SkyreaderGuild.IsOnlineLadderReady())
            {
                note.AddText(SkyreaderGuild.GetLadderUnavailableText(), FontColor.Warning);
                note.Build();
                return;
            }

            if (data == null)
            {
                note.AddText("The constellation board is updating...");
                note.Build();
                return;
            }

            if (data.SeasonId == null || data.Constellations == null || data.Constellations.Count == 0)
            {
                note.AddText("No season is active. The constellations are dark.");
                note.Build();
                return;
            }

            note.AddText(data.SeasonName ?? "Current Season", FontColor.Topic);
            note.Space();

            if (!string.IsNullOrEmpty(data.PlayerConstellationId))
            {
                var joined = data.Constellations.FirstOrDefault(c => c.Id == data.PlayerConstellationId);
                if (joined != null)
                {
                    note.AddText($"You follow {joined.Name.TagColor(FontColor.Good)}.");
                }
            }
            else
            {
                note.AddText("You have not chosen a constellation this season.", FontColor.Warning);
            }

            note.Space();
            note.AddHeaderTopic("Constellations");

            foreach (var constellation in data.Constellations)
            {
                string status = constellation.GoalsMet ? " ★".TagColor(FontColor.Good) : "";
                string name = constellation.Name + status;
                note.AddTopic("TopicLeft", name, $"{constellation.MemberCount} followers");
                note.AddText(constellation.Description, FontColor.Default);

                if (constellation.Goals != null)
                {
                    foreach (var goal in constellation.Goals)
                    {
                        int progress = 0;
                        if (constellation.Progress != null)
                            constellation.Progress.TryGetValue(goal.Key, out progress);
                        float pct = goal.Value > 0 ? 100f * progress / goal.Value : 100f;
                        string progressText = $"  {goal.Key}: {progress:N0}/{goal.Value:N0} ({pct:0.#}%)";
                        FontColor color = pct >= 100f ? FontColor.Good : FontColor.Default;
                        note.AddText(progressText, color);
                    }
                }

                if (string.IsNullOrEmpty(data.PlayerConstellationId))
                {
                    string capturedId = constellation.Id;
                    note.AddButton("Join " + constellation.Name, () => OnJoin(capturedId));
                }

                note.Space();
            }

            note.Build();
        }

        private void OnJoin(string constellationId)
        {
            SkyreaderGuild.OnlineClient?.JoinConstellation(constellationId, () =>
            {
                SkyreaderGuild.RefreshConstellations(force: true, onDone: OnFetchComplete);
            });
        }
    }
}

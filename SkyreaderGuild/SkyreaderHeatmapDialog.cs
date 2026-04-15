using System;
using System.Collections.Generic;
using System.Linq;

namespace SkyreaderGuild
{
    internal sealed class SkyreaderHeatmapDialog
    {
        private Dialog dialog;

        public static void Open()
        {
            new SkyreaderHeatmapDialog().Show();
        }

        private void Show()
        {
            dialog = Layer.Create<Dialog>();
            if (dialog.windows.Count > 0)
                dialog.windows[0].SetCaption("Astral Contamination Heatmap");

            dialog.textDetail.SetText("");
            dialog.spacer.SetActive(enable: false);
            SkyreaderGuild.RefreshHeatmap(force: false, onDone: OnFetchComplete);
            RebuildNote();
            dialog.AddButton("Refresh", Refresh, close: false);
            dialog.AddButton(Lang.Get("close"));
            ELayer.ui.AddLayer(dialog);
        }

        private void Refresh()
        {
            SkyreaderGuild.RefreshHeatmap(force: true, onDone: OnFetchComplete);
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

            var data = SkyreaderGuild.OnlineClient?.GetCachedHeatmap();
            UINote note = dialog.note;
            note.Clear();
            note.AddHeader("HeaderNote", "Astral Contamination Heatmap");

            if (!SkyreaderGuild.IsOnlineLadderReady())
            {
                note.AddText(SkyreaderGuild.GetLadderUnavailableText(), FontColor.Warning);
                note.Build();
                return;
            }

            if (data == null)
            {
                note.AddText(SkyreaderGuild.GetOnlineStatusText("The heatmap table is compiling town contamination reports..."), FontColor.Warning);
                note.Build();
                return;
            }

            if (data.SeasonName != null)
            {
                note.AddText(data.SeasonName, FontColor.Topic);
                note.Space();
            }

            note.AddText("Meteor-touched settlements and hubs warm the map. Astral Extractor cleansing cools the exact places where the sky has been disturbed.", FontColor.FoodQuality);
            note.Space();
            note.AddImage(SkyreaderHeatmapGraphic.Create(data));
            note.Space();
            note.AddHeaderTopic("Hottest Sites");

            List<SkyreaderOnlineClient.SiteHeat> sites = data.Sites == null
                ? new List<SkyreaderOnlineClient.SiteHeat>()
                : data.Sites
                    .Where(site => site != null)
                    .OrderBy(site => site.Ratio)
                    .ThenByDescending(site => site.Touched)
                    .ThenBy(site => site.SiteName ?? site.SiteId ?? "")
                    .ToList();

            if (sites.Count == 0)
            {
                note.AddText("No civilized sites have reported astral contamination yet.");
            }
            else
            {
                foreach (var site in sites)
                {
                    FontColor statusColor = GetStatusColor(site.Status);
                    string statusText = site.Status.TagColor(statusColor);
                    note.AddTopic("TopicLeft", site.SiteName, statusText);
                    note.AddText($"  Touched: {site.Touched:N0}  Cleansed: {site.Cleansed:N0}  ({site.Ratio * 100:0.#}% clear)");
                }
            }

            note.Space();
            note.AddText("Survey reports are gathered wherever meteor-touched NPCs or items appear in civilized zones. Cleansing those targets with the Astral Extractor lowers heat at that exact site.", FontColor.FoodQuality);

            note.Build();
        }

        private static FontColor GetStatusColor(string status)
        {
            switch (status)
            {
                case "Calm": return FontColor.Good;
                case "Stirring": return FontColor.Warning;
                case "Troubled": return FontColor.Bad;
                case "Overrun": return FontColor.Bad;
                default: return FontColor.Default;
            }
        }
    }
}

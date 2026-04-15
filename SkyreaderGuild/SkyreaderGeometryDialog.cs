using System;
using System.Collections.Generic;
using System.Linq;

namespace SkyreaderGuild
{
    internal sealed class SkyreaderGeometryDialog
    {
        private Dialog dialog;

        public static void Open()
        {
            new SkyreaderGeometryDialog().Show();
        }

        private void Show()
        {
            dialog = Layer.Create<Dialog>();
            if (dialog.windows.Count > 0)
                dialog.windows[0].SetCaption("Rift Geometry Orrery");

            dialog.textDetail.SetText("");
            dialog.spacer.SetActive(enable: false);
            SkyreaderGuild.RefreshGeometry(force: false, onDone: OnFetchComplete);
            RebuildNote();
            dialog.AddButton("Refresh", Refresh, close: false);
            dialog.AddButton(Lang.Get("close"));
            ELayer.ui.AddLayer(dialog);
        }

        private void Refresh()
        {
            SkyreaderGuild.RefreshGeometry(force: true, onDone: OnFetchComplete);
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

            var data = SkyreaderGuild.OnlineClient?.GetCachedGeometry();
            UINote note = dialog.note;
            note.Clear();
            note.AddHeader("HeaderNote", "Rift Geometry Orrery");

            if (!SkyreaderGuild.IsOnlineLadderReady())
            {
                note.AddText(SkyreaderGuild.GetLadderUnavailableText(), FontColor.Warning);
                note.Build();
                return;
            }

            if (data == null)
            {
                note.AddText("The orrery is calibrating...");
                note.Build();
                return;
            }

            if (data.SeasonName == null)
            {
                note.AddText("No season is active. The orrery is still.");
                note.Build();
                return;
            }

            note.AddText(data.SeasonName, FontColor.Topic);
            note.Space();
            note.AddTopic("TopicLeft", "Total Samples", data.TotalSamples.ToFormat());

            if (!string.IsNullOrEmpty(data.DominantShape))
            {
                note.AddTopic("TopicLeft", "Dominant Shape", data.DominantShape.TagColor(FontColor.Good));
                note.AddText(data.DominantFlavor);
            }
            else
            {
                note.AddText("No geometry data collected yet.");
            }

            if (!string.IsNullOrEmpty(data.GeometryBias))
            {
                note.Space();
                note.AddText($"Season Bias: {data.GeometryBias}".TagColor(FontColor.Topic));
            }

            if (data.Bands != null && data.Bands.Count > 0)
            {
                note.Space();
                note.AddHeaderTopic("Distribution by Danger Band");

                foreach (var band in data.Bands.OrderBy(b => int.Parse(b.Key)))
                {
                    string shapes = string.Join(", ", band.Value
                        .OrderByDescending(kv => kv.Value)
                        .Select(kv => $"{kv.Key} {kv.Value * 100:0.#}%"));
                    note.AddTopic("TopicLeft", $"Band {band.Key}", shapes);
                }
            }

            note.Build();
        }
    }
}

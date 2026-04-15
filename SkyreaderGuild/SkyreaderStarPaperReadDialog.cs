using System;
using System.Collections.Generic;
using UnityEngine;

namespace SkyreaderGuild
{
    internal sealed class SkyreaderStarPaperReadDialog
    {
        private enum ShelfView
        {
            Guild,
            Mine,
        }

        private const string ArkynFallbackId = "__arkyn_fallback__";

        private Dialog dialog;
        private UIButton refreshButton;
        private ShelfView currentView = ShelfView.Guild;
        private SkyreaderOnlineClient.StarPaperEntry selectedPaper;

        public static void Open()
        {
            new SkyreaderStarPaperReadDialog().Show();
        }

        private void Show()
        {
            dialog = Layer.Create<Dialog>();
            if (dialog.windows.Count > 0)
                dialog.windows[0].SetCaption("Star Paper Shelf");

            dialog.textDetail.SetText("");
            dialog.spacer.SetActive(enable: false);
            SkyreaderGuild.PullStarPapers(onDone: OnFetchComplete);
            RebuildNote();
            dialog.list.AddButton(null, "Refresh Guild Papers", Refresh, button => refreshButton = button);
            dialog.list.AddButton(null, Lang.Get("close"), dialog.Close);
            ELayer.ui.AddLayer(dialog);
        }

        private void Refresh()
        {
            if (currentView != ShelfView.Guild)
            {
                return;
            }

            selectedPaper = null;
            SkyreaderGuild.ForcePullStarPapers(onDone: OnFetchComplete);
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

            UpdateRefreshButton();

            UINote note = dialog.note;
            note.Clear();
            note.AddHeader("HeaderNote", "Star Papers");

            if (!SkyreaderGuild.IsOnlineLadderReady())
            {
                note.AddText(SkyreaderGuild.GetLadderUnavailableText(), FontColor.Warning);
                note.Build();
                return;
            }

            AddViewButtons(note);

            if (selectedPaper != null)
            {
                AddSelectedPaper(note);
            }
            else
            {
                AddPaperList(note);
            }

            note.Build();
        }

        private void UpdateRefreshButton()
        {
            if (refreshButton == null)
            {
                return;
            }

            bool enableRefresh = currentView == ShelfView.Guild;
            refreshButton.mainText.text = "Refresh Guild Papers";
            refreshButton.SetInteractableWithAlpha(enableRefresh);
        }

        private void AddViewButtons(UINote note)
        {
            note.AddButton(currentView == ShelfView.Guild ? "[Guild Papers]" : "Guild Papers", () => SwitchView(ShelfView.Guild));
            note.AddButton(currentView == ShelfView.Mine ? "[My Papers]" : "My Papers", () => SwitchView(ShelfView.Mine));
            note.Space();
        }

        private void SwitchView(ShelfView view)
        {
            currentView = view;
            selectedPaper = null;
            if (view == ShelfView.Guild)
            {
                SkyreaderGuild.PullStarPapers(onDone: OnFetchComplete);
            }
            else if (SkyreaderGuild.OnlineClient?.GetCachedOwnNotes() == null)
            {
                SkyreaderGuild.PullMyStarPapers(onDone: OnFetchComplete);
            }
            RebuildNote();
        }

        private void AddPaperList(UINote note)
        {
            List<SkyreaderOnlineClient.StarPaperEntry> papers = GetCurrentPapers();
            note.AddHeaderTopic(currentView == ShelfView.Guild ? "Guild Papers" : "My Papers");

            if (papers == null)
            {
                string loading = currentView == ShelfView.Guild
                    ? "The shelf is sorting fresh papers from across the guild..."
                    : "Your archived papers are being gathered...";
                note.AddText(loading);
                return;
            }

            if (papers.Count == 0)
            {
                string empty = currentView == ShelfView.Guild
                    ? "The shelf is empty. Star papers from other Skyreaders will appear here."
                    : "You have not submitted any star papers yet.";
                note.AddText(empty);
                return;
            }

            foreach (SkyreaderOnlineClient.StarPaperEntry paper in papers)
            {
                note.AddTopic("TopicLeft", paper.Title, $"Rating {paper.Rating}  {FormatDate(paper.CreatedAt)}");
                note.AddText(Preview(paper.Body));
                SkyreaderOnlineClient.StarPaperEntry captured = paper;
                note.AddButton("Read", () =>
                {
                    selectedPaper = captured;
                    RebuildNote();
                });
                note.Space();
            }
        }

        private void AddSelectedPaper(UINote note)
        {
            bool isMine = currentView == ShelfView.Mine;
            bool isFallback = IsArkynFallback(selectedPaper);

            note.AddButton("Back to List", () =>
            {
                selectedPaper = null;
                RebuildNote();
            });
            note.Space();

            note.AddHeaderTopic(selectedPaper.Title);
            note.AddText($"Rating: {selectedPaper.Rating}  Submitted: {FormatDate(selectedPaper.CreatedAt)}", selectedPaper.Rating >= 0 ? FontColor.Good : FontColor.Bad);
            note.Space();
            note.AddText(selectedPaper.Body);

            if (isMine)
            {
                note.Space();
                note.AddText("Your own papers are archived here for review. Voting is disabled for your submissions.", FontColor.FoodQuality);
                return;
            }

            if (isFallback)
            {
                note.Space();
                note.AddText("Arkyn left this as a thematic test paper for an otherwise empty shelf. It cannot be voted on.", FontColor.FoodQuality);
                return;
            }

            string capturedId = selectedPaper.Id;
            note.Space();
            note.AddButton("Upvote", () => Rate(capturedId, 1));
            note.AddButton("Downvote", () => Rate(capturedId, -1));
        }

        private List<SkyreaderOnlineClient.StarPaperEntry> GetCurrentPapers()
        {
            if (currentView == ShelfView.Guild)
            {
                return BuildGuildPaperList();
            }
            return SkyreaderGuild.OnlineClient?.GetCachedOwnNotes();
        }

        private List<SkyreaderOnlineClient.StarPaperEntry> BuildGuildPaperList()
        {
            List<SkyreaderOnlineClient.StarPaperEntry> papers = SkyreaderGuild.OnlineClient?.GetCachedNotes();
            if (papers == null)
            {
                return null;
            }

            if (papers.Count > 0)
            {
                return papers;
            }

            return new List<SkyreaderOnlineClient.StarPaperEntry> { CreateArkynFallbackPaper() };
        }

        private static SkyreaderOnlineClient.StarPaperEntry CreateArkynFallbackPaper()
        {
            return new SkyreaderOnlineClient.StarPaperEntry
            {
                Id = ArkynFallbackId,
                Title = "Arkyn's Trial Shelf Note",
                Body = "If you are reading this, the shelf still waits for its first true exchange. Spend two meteorite sources at the writing desk, record a title and your observations, and let another Skyreader find your paper among the stars.",
                Rating = 0,
                CreatedAt = "Arkyn's archive"
            };
        }

        private static bool IsArkynFallback(SkyreaderOnlineClient.StarPaperEntry paper)
        {
            return paper != null && string.Equals(paper.Id, ArkynFallbackId, StringComparison.Ordinal);
        }

        private void Rate(string noteId, int value)
        {
            selectedPaper = null;
            SkyreaderGuild.OnlineClient?.RateNote(noteId, value, () =>
            {
                SkyreaderGuild.ForcePullStarPapers(onDone: OnFetchComplete);
            });
            RebuildNote();
        }

        private static string Preview(string body)
        {
            if (string.IsNullOrWhiteSpace(body)) return "";
            string oneLine = string.Join(" ", body.Split(new[] { '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries));
            if (oneLine.Length <= 120) return oneLine;
            return oneLine.Substring(0, 117) + "...";
        }

        private static string FormatDate(string createdAt)
        {
            if (string.IsNullOrWhiteSpace(createdAt)) return "undated";
            return createdAt.Replace("T", " ").Replace("Z", " UTC");
        }
    }
}

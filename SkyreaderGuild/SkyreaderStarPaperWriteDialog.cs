using UnityEngine;
using UnityEngine.UI;

namespace SkyreaderGuild
{
    internal sealed class SkyreaderStarPaperWriteDialog
    {
        private const int SourceCost = 2;
        private const int TitleLimit = 80;
        private const int BodyLimit = 800;

        private Dialog dialog;
        private UIInputText titleInput;
        private UIInputText bodyInput;
        private UIButton sendButton;
        private bool submitting;

        public static void Open()
        {
            new SkyreaderStarPaperWriteDialog().Show();
        }

        private void Show()
        {
            if (!SkyreaderGuild.IsOnlineLadderReady())
            {
                Msg.SayRaw("The writing desk is inactive. Enable online features and join the guild first.");
                return;
            }

            if (SkyreaderGuild.GetCurrentRankValue() < (int)GuildRank.Seeker)
            {
                Msg.SayRaw("Arkyn's desk remains sealed to new members. Reach Seeker rank before submitting star research.");
                return;
            }

            if (SkyreaderGuild.CountMeteoriteSources() < SourceCost)
            {
                Msg.SayRaw($"Submitting star research requires {SourceCost} Meteorite Sources.");
                return;
            }

            dialog = Layer.Create<Dialog>("DialogInput");
            if (dialog.windows.Count > 0)
                dialog.windows[0].SetCaption("Star Paper Writing Desk");

            dialog.textDetail.SetText("");
            dialog.spacer.SetActive(enable: false);
            dialog.input.gameObject.SetActive(false);
            UIInputText prototype = dialog.input;
            dialog.input = null;

            dialog.note.Clear();
            dialog.note.AddHeader("HeaderNote", "Compose Star Paper");
            dialog.note.AddText($"Spend {SourceCost} Meteorite Sources to shelve a title and a full note for other Skyreaders.", FontColor.FoodQuality);
            dialog.note.Space();
            dialog.note.AddText("Write a title and record your observations before sending.", FontColor.Topic);

            Transform fieldContainer = BuildFieldContainer();
            titleInput = CreateField(prototype, fieldContainer, 56, TitleLimit, "Title");
            bodyInput = CreateField(prototype, fieldContainer, 144, BodyLimit, "Record your findings");
            bodyInput.field.lineType = InputField.LineType.MultiLineNewline;

            dialog.list.AddButton(null, $"Send ({SourceCost} Meteorite Sources)", Submit, button => sendButton = button);
            dialog.list.AddButton(null, Lang.Get("cancel"), dialog.Close);
            ELayer.ui.AddLayer(dialog);

            if (EClass.core != null)
            {
                EClass.core.actionsNextFrame.Add(() =>
                {
                    if (titleInput != null)
                        titleInput.Focus();
                });
            }
        }

        private Transform BuildFieldContainer()
        {
            GameObject container = new GameObject("StarPaperFields", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            container.transform.SetParent(dialog.layout.transform, worldPositionStays: false);
            container.transform.SetSiblingIndex(dialog.list.transform.GetSiblingIndex());

            RectTransform rect = container.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.sizeDelta = new Vector2(0f, 0f);

            VerticalLayoutGroup layout = container.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 10f;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.padding = new RectOffset(0, 0, 6, 6);

            ContentSizeFitter fitter = container.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            return container.transform;
        }

        private static UIInputText CreateField(UIInputText prototype, Transform parent, float preferredHeight, int characterLimit, string placeholder)
        {
            UIInputText field = Object.Instantiate(prototype.gameObject, parent).GetComponent<UIInputText>();
            field.gameObject.SetActive(true);
            field.Text = "";
            field.field.characterLimit = characterLimit;

            LayoutElement element = field.GetComponent<LayoutElement>();
            if (element == null)
            {
                element = field.gameObject.AddComponent<LayoutElement>();
            }
            element.preferredHeight = preferredHeight;
            element.minHeight = preferredHeight;

            Text placeholderText = field.field.placeholder as Text;
            if (placeholderText != null)
            {
                placeholderText.text = placeholder;
            }

            return field;
        }

        private void Submit()
        {
            if (submitting)
            {
                return;
            }

            string title = titleInput?.Text?.Trim();
            if (string.IsNullOrWhiteSpace(title) || title.Length < 3)
            {
                Msg.SayRaw("The title is too short.");
                return;
            }

            if (title.Length > TitleLimit)
            {
                Msg.SayRaw("The title is too long.");
                return;
            }

            string body = bodyInput?.Text?.Trim();
            if (string.IsNullOrWhiteSpace(body) || body.Length < 10)
            {
                Msg.SayRaw("The content is too short for a research note.");
                return;
            }

            if (body.Length > BodyLimit)
            {
                Msg.SayRaw("The content is too long for a research note.");
                return;
            }

            if (!SkyreaderGuild.ConsumeMeteoriteSources(SourceCost))
            {
                Msg.SayRaw($"Submitting star research requires {SourceCost} Meteorite Sources.");
                return;
            }

            submitting = true;
            if (sendButton != null)
            {
                sendButton.SetInteractableWithAlpha(false);
            }

            SkyreaderGuild.OnlineClient?.CreateNote(title, body, (success, message) =>
            {
                if (success)
                {
                    SkyreaderGuild.ForcePullMyStarPapers();
                    Msg.SayRaw("Your star paper has been shelved for other Skyreaders to discover.");
                    if (dialog != null && dialog.gameObject != null)
                    {
                        dialog.Close();
                    }
                    return;
                }

                SkyreaderGuild.RefundMeteoriteSources(SourceCost);
                submitting = false;
                if (sendButton != null)
                {
                    sendButton.SetInteractableWithAlpha(true);
                }

                Msg.SayRaw("The desk rejects the submission. Your Meteorite Sources have been returned.");
                if (!string.IsNullOrWhiteSpace(message))
                {
                    SkyreaderGuild.Log("Star paper rejection detail: " + message);
                }
            });
        }
    }
}

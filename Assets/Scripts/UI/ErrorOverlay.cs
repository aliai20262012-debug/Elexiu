using System;
using Nexus.Configuration;
using UnityEngine;
using UnityEngine.UI;

namespace Nexus.UI
{
    /// <summary>
    /// Full screen failure surface. Initialisation and scene load errors are always shown to
    /// the player with a retry path instead of leaving a black screen or a silent console error.
    /// </summary>
    public sealed class ErrorOverlay : MonoBehaviour
    {
        private Action _onRetry;
        private UiLabel _messageLabel;

        public static ErrorOverlay Show(string title, string message, Action onRetry, UiTheme theme)
        {
            Canvas canvas = UiFactory.CreateCanvas("ErrorCanvas", 500);
            ErrorOverlay overlay = canvas.gameObject.AddComponent<ErrorOverlay>();
            overlay.Build(theme != null ? theme : UiTheme.CreateDefault(), title, message, onRetry);
            return overlay;
        }

        public string MessageText
        {
            get { return _messageLabel != null ? _messageLabel.Text : string.Empty; }
        }

        private void Build(UiTheme theme, string title, string message, Action onRetry)
        {
            _onRetry = onRetry;

            RectTransform root = (RectTransform)transform;
            UiFactory.Stretch(root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            Image background = UiFactory.CreateImage(root, "Background", new Color(0.02f, 0.03f, 0.06f, 0.97f));
            background.raycastTarget = true;
            UiFactory.Stretch(background.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            Image panel = UiFactory.CreateImage(root, "Panel", theme.PanelNavy);
            UiFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(1040f, 520f), Vector2.zero);

            Image accent = UiFactory.CreateImage(panel.rectTransform, "Accent", theme.Danger);
            UiFactory.Place(accent.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(6f, 520f), Vector2.zero);

            UiLabel heading = UiFactory.CreateLabel(
                panel.rectTransform, "Heading", title, theme.SectionFontSize * 1.5f, theme.Danger, UiTextAlignment.Left, 10f);
            UiFactory.Place(heading.RectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(900f, 56f), new Vector2(48f, -48f));

            _messageLabel = UiFactory.CreateLabel(
                panel.rectTransform, "Message", message, theme.BodyFontSize, theme.TextPrimary, UiTextAlignment.Left, 2f);
            UiFactory.Place(_messageLabel.RectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(944f, 300f), new Vector2(48f, -128f));

            UiLabel hint = UiFactory.CreateLabel(
                panel.rectTransform,
                "Hint",
                "Check the Console for the full call stack. Failures here come from service composition, the simulation core or scene navigation.",
                theme.BodyFontSize * 0.85f,
                theme.TextMuted,
                UiTextAlignment.Left,
                2f);
            UiFactory.Place(hint.RectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(944f, 72f), new Vector2(48f, 108f));

            if (onRetry != null)
            {
                UiButton retry = UiFactory.CreateButton(panel.rectTransform, "RetryButton", "RETRY", theme);
                UiFactory.Place(retry.RectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(240f, 64f), new Vector2(-48f, 44f));
                retry.Button.onClick.AddListener(HandleRetry);
            }
        }

        private void HandleRetry()
        {
            Action retry = _onRetry;
            _onRetry = null;

            if (retry != null)
            {
                retry();
            }
        }
    }
}

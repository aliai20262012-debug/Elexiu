using Nexus.Configuration;
using UnityEngine;
using UnityEngine.UI;

namespace Nexus.UI
{
    /// <summary>Splash/loading surface shown while application services are initialised.</summary>
    public sealed class BootstrapLoadingView
    {
        private readonly UiLabel _statusLabel;
        private readonly RectTransform _progressFill;

        public Canvas Canvas { get; private set; }

        public BootstrapLoadingView(UiTheme theme)
        {
            Canvas = UiFactory.CreateCanvas("BootstrapCanvas", 10);

            RectTransform root = (RectTransform)Canvas.transform;
            Image background = UiFactory.CreateImage(root, "Background", theme.BackgroundDeepNavy);
            UiFactory.Stretch(background.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            UiLabel title = UiFactory.CreateLabel(
                root, "Title", "NEXUS CIVILIZATION", theme.TitleFontSize * 0.55f, theme.PrimaryCyan, UiTextAlignment.Center, 14f);
            UiFactory.Place(title.RectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(1400f, 90f), new Vector2(0f, 60f));

            _statusLabel = UiFactory.CreateLabel(
                root, "Status", "INITIALIZING SYSTEMS", theme.BodyFontSize, theme.TextMuted, UiTextAlignment.Center, 8f);
            UiFactory.Place(_statusLabel.RectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(1200f, 36f), new Vector2(0f, -10f));

            Image track = UiFactory.CreateImage(root, "ProgressTrack", new Color(1f, 1f, 1f, 0.08f));
            UiFactory.Place(track.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(520f, 4f), new Vector2(0f, -60f));

            Image fill = UiFactory.CreateImage(track.rectTransform, "ProgressFill", theme.AccentGreen);
            _progressFill = fill.rectTransform;
            UiFactory.Stretch(
                _progressFill,
                new Vector2(0f, 0f),
                new Vector2(0.35f, 1f),
                Vector2.zero,
                Vector2.zero);

            UiLabel footer = UiFactory.CreateLabel(
                root, "Footer", "CORE SIMULATION BUILD  ·  PHASE 02", theme.BodyFontSize * 0.8f, theme.TextMuted, UiTextAlignment.Center, 4f);
            UiFactory.Place(footer.RectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(1200f, 30f), new Vector2(0f, 48f));
        }

        public void SetStatus(string status)
        {
            if (_statusLabel != null)
            {
                _statusLabel.Text = string.IsNullOrEmpty(status) ? string.Empty : status.ToUpperInvariant();
            }
        }

        public void SetProgress(float normalized)
        {
            if (_progressFill == null)
            {
                return;
            }

            Vector2 anchorMax = _progressFill.anchorMax;
            anchorMax.x = Mathf.Clamp01(normalized);
            _progressFill.anchorMax = anchorMax;
        }

        public string StatusText
        {
            get { return _statusLabel != null ? _statusLabel.Text : string.Empty; }
        }
    }
}

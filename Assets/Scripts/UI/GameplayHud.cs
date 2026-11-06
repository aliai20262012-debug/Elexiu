using Nexus.Configuration;
using Nexus.Core.Platform;
using UnityEngine;
using UnityEngine.UI;

namespace Nexus.UI
{
    /// <summary>
    /// Gameplay shell HUD. Deliberately minimal: it demonstrates the presentation layer and
    /// provides the exit path back to the main menu. Real HUDs (resources, diplomacy, tech)
    /// arrive with their gameplay systems.
    /// </summary>
    public sealed class GameplayHud
    {
        private readonly UiLabel _tickLabel;
        private readonly UiLabel _statusLabel;

        public Canvas Canvas { get; private set; }

        public UiButton ReturnToMenuButton { get; private set; }

        public GameplayHud(UiTheme theme, GameConfiguration configuration, IPlatformInfo platform)
        {
            Canvas = UiFactory.CreateCanvas("GameplayCanvas", 20);

            RectTransform safeArea = UiFactory.CreateRect(Canvas.transform, "SafeArea");
            UiFactory.Stretch(safeArea, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            safeArea.gameObject.AddComponent<SafeAreaFitter>();

            Image topBar = UiFactory.CreateImage(safeArea, "TopBar", theme.PanelNavy);
            UiFactory.Stretch(
                topBar.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, -76f),
                Vector2.zero);

            UiLabel title = UiFactory.CreateLabel(
                safeArea,
                "Title",
                configuration.GameTitle + " // SIMULATION PLACEHOLDER",
                theme.SectionFontSize,
                theme.PrimaryCyan,
                UiTextAlignment.Left,
                8f);
            UiFactory.Place(title.RectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(1100f, 40f), new Vector2(40f, -46f));

            UiLabel platformLabel = UiFactory.CreateLabel(
                safeArea,
                "Platform",
                platform.Kind.ToString().ToUpperInvariant(),
                theme.BodyFontSize,
                theme.TextMuted,
                UiTextAlignment.Right,
                4f);
            UiFactory.Place(platformLabel.RectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(500f, 40f), new Vector2(-40f, -46f));

            _tickLabel = UiFactory.CreateLabel(
                safeArea, "Ticks", "TICK 000000", theme.ButtonFontSize, theme.AccentGreen, UiTextAlignment.Left, 6f);
            UiFactory.Place(_tickLabel.RectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(600f, 48f), new Vector2(40f, 40f));

            _statusLabel = UiFactory.CreateLabel(
                safeArea,
                "Status",
                "SIMULATION CLOCK RUNNING // NO DOMAIN SYSTEMS REGISTERED YET",
                theme.BodyFontSize,
                theme.TextMuted,
                UiTextAlignment.Left,
                2f);
            UiFactory.Place(_statusLabel.RectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(1000f, 32f), new Vector2(40f, 92f));

            ReturnToMenuButton = UiFactory.CreateButton(safeArea, "ReturnToMenuButton", "RETURN TO MENU", theme);
            UiFactory.Place(ReturnToMenuButton.RectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(320f, 68f), new Vector2(-40f, 40f));
        }

        public void SetTick(long tick)
        {
            if (_tickLabel != null)
            {
                _tickLabel.Text = "TICK " + tick.ToString("000000");
            }
        }

        public void SetStatus(string status)
        {
            if (_statusLabel != null)
            {
                _statusLabel.Text = status;
            }
        }
    }
}

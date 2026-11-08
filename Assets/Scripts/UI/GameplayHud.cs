using Nexus.Configuration;
using Nexus.Core.Platform;
using Nexus.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace Nexus.UI
{
    /// <summary>
    /// Gameplay shell HUD: brand header plus the simulation control surface. Deliberately
    /// minimal - real HUDs (resources, diplomacy, technology) arrive with their domain
    /// systems. The view owns layout only; every button raises an intent that the gameplay
    /// controller translates into a session/simulation operation.
    /// </summary>
    public sealed class GameplayHud
    {
        private readonly UiTheme _theme;

        public Canvas Canvas { get; private set; }

        public SimulationControlBar ControlBar { get; private set; }

        public UiButton ReturnToMenuButton { get; private set; }

        /// <summary>Region dossier; hidden until the player selects a region.</summary>
        public RegionInfoPanel RegionPanel { get; private set; }

        public GameplayHud(UiTheme theme, GameConfiguration configuration, IPlatformInfo platform)
        {
            _theme = theme;

            UiLayoutProfile layout = platform != null ? platform.Layout : UiLayoutProfile.Regular;
            bool compact = layout == UiLayoutProfile.Compact;

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

            Image topAccent = UiFactory.CreateImage(
                safeArea,
                "TopBarAccent",
                new Color(theme.PrimaryCyan.r, theme.PrimaryCyan.g, theme.PrimaryCyan.b, 0.55f));
            UiFactory.Stretch(
                topAccent.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, -78f),
                new Vector2(0f, -76f));

            UiLabel title = UiFactory.CreateLabel(
                safeArea,
                "Title",
                configuration.GameTitle + " // SIMULATION CORE",
                compact ? 20f : theme.SectionFontSize,
                theme.PrimaryCyan,
                UiTextAlignment.Left,
                6f);
            UiFactory.Stretch(
                title.RectTransform,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(compact ? 24f : 40f, -66f),
                new Vector2(compact ? -270f : -480f, -38f));

            UiLabel platformLabel = UiFactory.CreateLabel(
                safeArea,
                "Platform",
                (platform != null ? platform.Kind.ToString().ToUpperInvariant() : "UNKNOWN") +
                " // " + (platform != null ? platform.Layout.ToString().ToUpperInvariant() : "REGULAR"),
                compact ? 13f : theme.BodyFontSize,
                theme.TextMuted,
                UiTextAlignment.Right,
                3f);
            UiFactory.Place(
                platformLabel.RectTransform,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(compact ? 240f : 420f, 30f),
                new Vector2(compact ? -24f : -40f, -46f));

            ControlBar = new SimulationControlBar(safeArea, theme, configuration, platform);
            RegionPanel = new RegionInfoPanel(safeArea, theme, platform, ControlBar.BarHeight + 12f);

            ReturnToMenuButton = UiFactory.CreateButton(
                ControlBar.ActionRow,
                "ReturnToMenuButton",
                "RETURN TO MENU",
                theme);
            ReturnToMenuButton.Label.SetFontSize(compact ? 16f : 18f);

            LayoutElement menuLayout = ReturnToMenuButton.RectTransform.GetComponent<LayoutElement>();
            if (menuLayout == null)
            {
                menuLayout = ReturnToMenuButton.RectTransform.gameObject.AddComponent<LayoutElement>();
            }

            menuLayout.preferredWidth = compact ? 186f : 220f;
            menuLayout.minWidth = compact ? 186f : 220f;
            menuLayout.preferredHeight = compact ? 52f : 60f;
            menuLayout.minHeight = compact ? 52f : 60f;
            menuLayout.flexibleWidth = 0f;
            menuLayout.flexibleHeight = 0f;

            ControlBar.SetStatus(SimulationStatus.Stopped, SimulationSpeed.Normal);
            ControlBar.SetSession(null);
            ControlBar.SetTime(0.0, 0L);
        }

        /// <summary>Updates the clock readout (simulated seconds and tick number).</summary>
        public void SetSimulationTime(double elapsedSeconds, long tick)
        {
            if (ControlBar != null)
            {
                ControlBar.SetTime(elapsedSeconds, tick);
            }
        }

        /// <summary>Updates the lifecycle indicator and the pause/speed button states.</summary>
        public void SetSimulationStatus(SimulationStatus status, double speedMultiplier)
        {
            if (ControlBar != null)
            {
                ControlBar.SetStatus(status, speedMultiplier);
            }
        }

        public void SetSession(GameSession session)
        {
            if (ControlBar != null)
            {
                ControlBar.SetSession(session);
            }
        }

        /// <summary>Shows a message instead of the lifecycle text (errors, scene transitions).</summary>
        public void SetStatus(string status)
        {
            if (ControlBar != null)
            {
                ControlBar.SetMessage(status);
            }
        }

        /// <summary>Kept for Phase 01 compatibility; timestamps are derived from elapsed time.</summary>
        public void SetTick(long tick)
        {
            if (ControlBar != null)
            {
                ControlBar.SetTick(tick);
            }
        }

        public UiTheme Theme
        {
            get { return _theme; }
        }
    }
}

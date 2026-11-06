using Nexus.Configuration;
using Nexus.Core.Platform;
using Nexus.Settings;
using UnityEngine;
using UnityEngine.UI;

namespace Nexus.UI
{
    /// <summary>
    /// Presentation layer of the main menu. It only builds widgets and exposes them; all
    /// behaviour (state transitions, scene flow) lives in the controller. That separation is
    /// what allows the menu to be restyled or replaced without touching application logic.
    /// </summary>
    public sealed class MainMenuView
    {
        public Canvas Canvas { get; private set; }

        public UiButton NewGameButton { get; private set; }

        public UiButton ContinueButton { get; private set; }

        public UiButton SettingsButton { get; private set; }

        public UiButton ExitButton { get; private set; }

        public UiLabel TitleLabel { get; private set; }

        public UiLabel StatusLabel { get; private set; }

        public SettingsPanel Settings { get; private set; }

        public MainMenuView(
            UiTheme theme,
            GameConfiguration configuration,
            IPlatformInfo platform,
            ISettingsService settings,
            System.Action onSettingsClosed)
        {
            Canvas = UiFactory.CreateCanvas("MainMenuCanvas");

            RectTransform safeArea = UiFactory.CreateRect(Canvas.transform, "SafeArea");
            UiFactory.Stretch(safeArea, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            safeArea.gameObject.AddComponent<SafeAreaFitter>();

            Image background = UiFactory.CreateImage(safeArea, "Background", theme.BackgroundDeepNavy);
            UiFactory.Stretch(background.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            BuildChrome(safeArea, theme, configuration, platform);

            RectTransform column = UiFactory.CreateRect(safeArea, "CenterColumn");
            UiFactory.Place(column, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(960f, 760f), new Vector2(0f, 12f));

            BuildTitle(column, theme, configuration);
            BuildButtons(column, theme);

            StatusLabel = UiFactory.CreateLabel(
                column, "Status", "AWAITING COMMAND", theme.BodyFontSize, theme.TextMuted, UiTextAlignment.Center, 6f);
            UiFactory.Place(StatusLabel.RectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(900f, 32f), new Vector2(0f, -736f));

            Settings = new SettingsPanel(safeArea, theme, settings, onSettingsClosed);
        }

        public void SetStatus(string status)
        {
            if (StatusLabel != null)
            {
                StatusLabel.Text = string.IsNullOrEmpty(status) ? string.Empty : status.ToUpperInvariant();
            }
        }

        public void SetContinueAvailable(bool available, UiTheme theme)
        {
            if (ContinueButton == null)
            {
                return;
            }

            ContinueButton.Interactable = available;
            ContinueButton.Label.SetColor(available ? theme.TextPrimary : theme.TextMuted);
        }

        public void SetInteractable(bool interactable)
        {
            SetButtonState(NewGameButton, interactable);
            SetButtonState(SettingsButton, interactable);
            SetButtonState(ExitButton, interactable);
        }

        private static void SetButtonState(UiButton button, bool interactable)
        {
            if (button != null)
            {
                button.Interactable = interactable;
            }
        }

        private void BuildChrome(RectTransform parent, UiTheme theme, GameConfiguration configuration, IPlatformInfo platform)
        {
            Image topAccent = UiFactory.CreateImage(parent, "TopAccent", theme.PrimaryCyan);
            topAccent.color = new Color(theme.PrimaryCyan.r, theme.PrimaryCyan.g, theme.PrimaryCyan.b, 0.55f);
            UiFactory.Stretch(
                topAccent.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, -3f),
                Vector2.zero);

            UiLabel brand = UiFactory.CreateLabel(
                parent, "TopBarLeft", "NEXUS CIVILIZATION // COMMAND NETWORK", theme.BodyFontSize, theme.TextMuted, UiTextAlignment.Left, 6f);
            UiFactory.Place(brand.RectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(900f, 36f), new Vector2(56f, -44f));

            UiLabel version = UiFactory.CreateLabel(
                parent,
                "TopBarRight",
                "BUILD " + configuration.BuildVersion + "  ·  " + configuration.BuildChannel,
                theme.BodyFontSize,
                theme.TextMuted,
                UiTextAlignment.Right,
                4f);
            UiFactory.Place(version.RectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(760f, 36f), new Vector2(-56f, -44f));

            string footerText = "NEXUS CIVILIZATION STUDIO  ·  UNITY 6.3 LTS  ·  " + platform.Kind.ToString().ToUpperInvariant();
            UiLabel footer = UiFactory.CreateLabel(
                parent, "Footer", footerText, theme.BodyFontSize * 0.85f, theme.TextMuted, UiTextAlignment.Center, 4f);
            UiFactory.Place(footer.RectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(1400f, 32f), new Vector2(0f, 34f));

            Image bottomAccent = UiFactory.CreateImage(parent, "BottomAccent", theme.AccentGreen);
            bottomAccent.color = new Color(theme.AccentGreen.r, theme.AccentGreen.g, theme.AccentGreen.b, 0.35f);
            UiFactory.Stretch(
                bottomAccent.rectTransform,
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                Vector2.zero,
                new Vector2(0f, 2f));
        }

        private void BuildTitle(RectTransform column, UiTheme theme, GameConfiguration configuration)
        {
            TitleLabel = UiFactory.CreateLabel(
                column,
                "Title",
                "NEXUS\nCIVILIZATION",
                theme.TitleFontSize,
                theme.PrimaryCyan,
                UiTextAlignment.Center,
                theme.TitleCharacterSpacing);
            UiFactory.Place(TitleLabel.RectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(960f, 250f), Vector2.zero);

            UiLabel subtitle = UiFactory.CreateLabel(
                column,
                "Subtitle",
                configuration.BuildChannel + " BUILD  ·  PHASE 01 FOUNDATION",
                theme.SectionFontSize,
                theme.TextPrimary,
                UiTextAlignment.Center,
                8f);
            UiFactory.Place(subtitle.RectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(900f, 40f), new Vector2(0f, -262f));

            Image divider = UiFactory.CreateImage(column, "Divider", theme.PrimaryCyan);
            divider.color = new Color(theme.PrimaryCyan.r, theme.PrimaryCyan.g, theme.PrimaryCyan.b, 0.5f);
            UiFactory.Place(divider.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(520f, 2f), new Vector2(0f, -320f));
        }

        private void BuildButtons(RectTransform column, UiTheme theme)
        {
            VerticalLayoutGroup stack = UiFactory.CreateVerticalStack(column, "MenuStack", 18f);
            UiFactory.Place(((RectTransform)stack.transform), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(460f, 352f), new Vector2(0f, -354f));

            NewGameButton = UiFactory.CreateButton(stack.transform as RectTransform, "NewGameButton", "NEW GAME", theme);
            ContinueButton = UiFactory.CreateButton(stack.transform as RectTransform, "ContinueButton", "CONTINUE", theme);
            SettingsButton = UiFactory.CreateButton(stack.transform as RectTransform, "SettingsButton", "SETTINGS", theme);
            ExitButton = UiFactory.CreateButton(stack.transform as RectTransform, "ExitButton", "EXIT", theme);

            ContinueButton.Interactable = false;
            ContinueButton.Label.SetColor(theme.TextMuted);
        }
    }
}

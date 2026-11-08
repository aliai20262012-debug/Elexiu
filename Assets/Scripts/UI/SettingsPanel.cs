using System;
using Nexus.Configuration;
using Nexus.Settings;
using UnityEngine;
using UnityEngine.UI;

namespace Nexus.UI
{
    /// <summary>
    /// Modal settings overlay: display quality and master volume. Phase 01 scope only;
    /// graphics toggles (resolution, UI scale, accessibility) arrive later.
    /// </summary>
    public sealed class SettingsPanel
    {
        private readonly ISettingsService _settings;
        private readonly RectTransform _root;
        private readonly UiLabel _qualityValue;
        private readonly UiLabel _volumeValue;

        public bool IsVisible
        {
            get { return _root != null && _root.gameObject.activeSelf; }
        }

        public SettingsPanel(RectTransform parent, UiTheme theme, ISettingsService settings, Action onClosed)
        {
            _settings = settings;

            Image overlay = UiFactory.CreateImage(parent, "SettingsOverlay", new Color(0.01f, 0.02f, 0.04f, 0.82f));
            overlay.raycastTarget = true;
            _root = overlay.rectTransform;
            UiFactory.Stretch(_root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            Image panel = UiFactory.CreateImage(_root, "Panel", theme.PanelNavy);
            panel.raycastTarget = true;
            RectTransform panelRect = panel.rectTransform;
            UiFactory.Place(panelRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(760f, 520f), Vector2.zero);

            UiLabel title = UiFactory.CreateLabel(
                panelRect, "Title", "SETTINGS", theme.SectionFontSize * 1.4f, theme.PrimaryCyan, UiTextAlignment.Left, 10f);
            UiFactory.Place(title.RectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(600f, 48f), new Vector2(48f, -40f));

            Image divider = UiFactory.CreateImage(panelRect, "Divider", theme.PrimaryCyan);
            divider.color = new Color(theme.PrimaryCyan.r, theme.PrimaryCyan.g, theme.PrimaryCyan.b, 0.45f);
            UiFactory.Place(divider.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(664f, 2f), new Vector2(48f, -96f));

            _qualityValue = BuildRow(panelRect, theme, "QUALITY", 0f, OnQualityDown, OnQualityUp);
            _volumeValue = BuildRow(panelRect, theme, "MASTER VOLUME", -92f, OnVolumeDown, OnVolumeUp);

            UiButton back = UiFactory.CreateButton(panelRect, "BackButton", "BACK", theme);
            UiFactory.Place(back.RectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(300f, 64f), new Vector2(0f, 44f));
            back.Button.onClick.AddListener(() =>
            {
                SetVisible(false);
                if (onClosed != null)
                {
                    onClosed();
                }
            });

            SetVisible(false);
            Refresh();
        }

        public void SetVisible(bool visible)
        {
            _root.gameObject.SetActive(visible);

            if (visible)
            {
                Refresh();
            }
        }

        public void Refresh()
        {
            _qualityValue.Text = _settings.QualityLevelName.ToUpperInvariant();
            _volumeValue.Text = Mathf.RoundToInt(_settings.MasterVolume * 100f) + "%";
        }

        private UiLabel BuildRow(
            RectTransform parent,
            UiTheme theme,
            string labelText,
            float yOffset,
            UnityEngine.Events.UnityAction onDecrease,
            UnityEngine.Events.UnityAction onIncrease)
        {
            UiLabel label = UiFactory.CreateLabel(
                parent, labelText + "Label", labelText, theme.BodyFontSize, theme.TextMuted, UiTextAlignment.Left, 6f);
            UiFactory.Place(label.RectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(300f, 40f), new Vector2(48f, -140f + yOffset));

            UiLabel value = UiFactory.CreateLabel(
                parent, labelText + "Value", string.Empty, theme.ButtonFontSize, theme.TextPrimary, UiTextAlignment.Center, 4f);
            UiFactory.Place(value.RectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(220f, 56f), new Vector2(40f, -132f + yOffset));

            UiButton decrease = UiFactory.CreateButton(parent, labelText + "Decrease", "-", theme);
            UiFactory.Place(decrease.RectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(120f, 56f), new Vector2(-190f, -132f + yOffset));
            decrease.Button.onClick.AddListener(onDecrease);

            UiButton increase = UiFactory.CreateButton(parent, labelText + "Increase", "+", theme);
            UiFactory.Place(increase.RectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(120f, 56f), new Vector2(-48f, -132f + yOffset));
            increase.Button.onClick.AddListener(onIncrease);

            return value;
        }

        private void OnQualityDown()
        {
            _settings.CycleQuality(-1);
            Refresh();
        }

        private void OnQualityUp()
        {
            _settings.CycleQuality(1);
            Refresh();
        }

        private void OnVolumeDown()
        {
            _settings.AdjustMasterVolume(-0.1f);
            Refresh();
        }

        private void OnVolumeUp()
        {
            _settings.AdjustMasterVolume(0.1f);
            Refresh();
        }
    }
}

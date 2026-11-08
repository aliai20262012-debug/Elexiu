using System;
using System.Text;
using Nexus.Configuration;
using Nexus.Core.Platform;
using Nexus.Planet;
using UnityEngine;
using UnityEngine.UI;

namespace Nexus.UI
{
    /// <summary>
    /// Region dossier shown when the player selects a region: identity, terrain, climate,
    /// resources, environment and habitability, plus the two view commands.
    ///
    /// Presentation only - it renders data handed to it and raises intents; the planet scene
    /// controller decides what those intents do.
    /// </summary>
    public sealed class RegionInfoPanel
    {
        private readonly StringBuilder _builder = new StringBuilder(512);
        private readonly RectTransform _root;
        private readonly Image _background;
        private readonly Image _accent;
        private readonly UiLabel _header;
        private readonly UiLabel _name;
        private readonly UiLabel _live;
        private readonly UiLabel _body;
        private readonly UiButton _focusButton;
        private readonly UiButton _backButton;
        private readonly bool _compact;

        private PlanetRegion _region;
        private float _temperatureC;
        private string _seasonLabel = "NORTHERN SUMMER";
        private long _dayNumber;

        /// <summary>Raised when the player asks to fly to the selected region.</summary>
        public event Action FocusRequested;

        /// <summary>Raised when the player asks to return to the planet overview.</summary>
        public event Action OverviewRequested;

        public RegionInfoPanel(
            RectTransform parent,
            UiTheme theme,
            IPlatformInfo platform,
            float bottomInset)
        {
            _compact = platform != null && platform.Layout == UiLayoutProfile.Compact;

            float width = _compact ? 340f : 386f;
            float height = _compact ? 430f : 560f;
            float right = _compact ? -20f : -40f;
            float bottom = bottomInset + (_compact ? 14f : 24f);

            _background = UiFactory.CreateImage(parent, "RegionPanel", theme.PanelNavy);
            _background.raycastTarget = true;
            _root = _background.rectTransform;
            UiFactory.Place(
                _root,
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(width, height),
                new Vector2(right, bottom));

            _accent = UiFactory.CreateImage(
                _root,
                "RegionAccent",
                new Color(theme.PrimaryCyan.r, theme.PrimaryCyan.g, theme.PrimaryCyan.b, 0.9f));
            UiFactory.Stretch(
                _accent.rectTransform,
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
                Vector2.zero,
                new Vector2(3f, 0f));

            _header = UiFactory.CreateLabel(
                _root,
                "RegionHeader",
                "REGION // --",
                _compact ? 13f : 14f,
                theme.PrimaryCyan,
                UiTextAlignment.Left,
                4f);
            UiFactory.Place(
                _header.RectTransform,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(width - 36f, 20f),
                new Vector2(20f, -18f));

            _name = UiFactory.CreateLabel(
                _root,
                "RegionName",
                "NO REGION SELECTED",
                _compact ? 22f : 26f,
                theme.TextPrimary,
                UiTextAlignment.Left,
                2f);
            UiFactory.Place(
                _name.RectTransform,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(width - 36f, 34f),
                new Vector2(20f, -42f));

            _live = UiFactory.CreateLabel(
                _root,
                "RegionLive",
                "LIVE // --",
                _compact ? 12f : 13f,
                theme.AccentGreen,
                UiTextAlignment.Left,
                3f);
            UiFactory.Place(
                _live.RectTransform,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(width - 36f, 18f),
                new Vector2(20f, -80f));

            Image divider = UiFactory.CreateImage(
                _root,
                "RegionDivider",
                new Color(theme.TextMuted.r, theme.TextMuted.g, theme.TextMuted.b, 0.35f));
            UiFactory.Place(
                divider.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(width - 40f, 1f),
                new Vector2(20f, -104f));

            _body = UiFactory.CreateLabel(
                _root,
                "RegionBody",
                string.Empty,
                _compact ? 12f : 14f,
                theme.TextPrimary,
                UiTextAlignment.Left,
                1f);
            UiFactory.Place(
                _body.RectTransform,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(width - 40f, height - 230f),
                new Vector2(20f, -116f));

            RectTransform actions = UiFactory.CreateRect(_root, "RegionActions");
            UiFactory.Place(
                actions,
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(width - 32f, 54f),
                new Vector2(16f, 16f));

            HorizontalLayoutGroup row = actions.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 10f;
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = true;
            row.childForceExpandHeight = true;

            _focusButton = UiFactory.CreateButton(actions, "RegionFocusButton", "ENTER REGION", theme);
            _focusButton.Label.SetFontSize(_compact ? 15f : 17f);
            _focusButton.ApplyPrimaryStyle(theme.PrimaryCyan);

            _backButton = UiFactory.CreateButton(actions, "RegionBackButton", "OVERVIEW", theme);
            _backButton.Label.SetFontSize(_compact ? 15f : 17f);
            _backButton.ApplyPrimaryStyle(theme.TextPrimary);

            _focusButton.Button.onClick.AddListener(OnFocusClicked);
            _backButton.Button.onClick.AddListener(OnBackClicked);

            SetVisible(false);
        }

        public RectTransform Root
        {
            get { return _root; }
        }

        public bool IsVisible
        {
            get { return _root != null && _root.gameObject.activeSelf; }
        }

        public PlanetRegion Region
        {
            get { return _region; }
        }

        /// <summary>Header line, exposed for tests.</summary>
        public string HeaderText
        {
            get { return _header.Text; }
        }

        public string NameText
        {
            get { return _name.Text; }
        }

        public string BodyText
        {
            get { return _body.Text; }
        }

        public string LiveText
        {
            get { return _live.Text; }
        }

        public UiButton FocusButton
        {
            get { return _focusButton; }
        }

        public UiButton BackButton
        {
            get { return _backButton; }
        }

        public void SetVisible(bool visible)
        {
            if (_root != null)
            {
                _root.gameObject.SetActive(visible);
            }
        }

        /// <summary>Shows the dossier for a region and enables the view actions.</summary>
        public void Show(PlanetRegion region)
        {
            _region = region;
            if (region == null)
            {
                Hide();
                return;
            }

            _header.Text = "REGION // " + region.Code;
            _name.Text = region.Name;

            Color accent = PlanetSurface.AccentFor(region);
            accent.a = 0.9f;
            _accent.color = accent;
            _body.Text = BuildBody(region);
            UpdateLiveLine();
            SetVisible(true);
        }

        public void Hide()
        {
            _region = null;
            SetVisible(false);
        }

        /// <summary>Updates the live environment line (called from planet day events).</summary>
        public void SetEnvironment(float temperatureC, string seasonLabel, long dayNumber)
        {
            _temperatureC = temperatureC;
            _seasonLabel = string.IsNullOrEmpty(seasonLabel) ? "NORTHERN SUMMER" : seasonLabel;
            _dayNumber = dayNumber;
            UpdateLiveLine();
        }

        private void UpdateLiveLine()
        {
            _live.Text = "LIVE // " + PlanetLabels.PlanetDay(_dayNumber) + " // " + _seasonLabel +
                         (_region != null ? " // " + PlanetLabels.Temperature(_temperatureC) : string.Empty);
        }

        private string BuildBody(PlanetRegion region)
        {
            _builder.Length = 0;

            AppendLine(region.TerrainLabel + "   //   " + region.WaterLabel);
            AppendLine(PlanetLabels.Coordinate(region.LatitudeDegrees, region.LongitudeDegrees) +
                       "   //   LAND " + PlanetLabels.Percent(region.LandFraction));
            AppendLine(string.Empty);

            AppendLine("CLIMATE");
            AppendLine("  MEAN " + PlanetLabels.Temperature(region.Climate.MeanTemperatureC) +
                       "   RANGE " + region.Climate.TemperatureRangeC.ToString("0.0") + " °C");
            AppendLine("  HUMIDITY " + PlanetLabels.Percent(region.Climate.Humidity) +
                       "   WIND " + PlanetLabels.KilometresPerHour(region.Climate.WindKph));
            AppendLine("  PRECIPITATION " + PlanetLabels.Millimetres(region.Climate.PrecipitationMm));
            AppendLine(string.Empty);

            AppendLine("RESOURCES");
            if (region.Resources == null || region.Resources.Count == 0)
            {
                AppendLine("  NONE SURVEYED");
            }
            else
            {
                for (int i = 0; i < region.Resources.Count; i++)
                {
                    AppendLine("  " + PlanetLabels.Resource(region.Resources[i].Type) + "  " +
                               PlanetLabels.Percent(region.Resources[i].Abundance));
                }
            }

            AppendLine(string.Empty);
            AppendLine("ENVIRONMENT");
            AppendLine("  AIR " + PlanetLabels.Percent(region.Environment.AirQuality) +
                       "   RADIATION " + PlanetLabels.Percent(region.Environment.Radiation));
            AppendLine("  BIODIVERSITY " + PlanetLabels.Percent(region.Environment.Biodiversity) +
                       "   TECTONIC " + PlanetLabels.Percent(region.Environment.TectonicActivity));
            AppendLine(string.Empty);

            AppendLine("HABITABILITY " + region.Habitability.ScorePercent + "%  //  " + region.Habitability.Rating);
            AppendLine("LIMITING FACTOR: " + region.Habitability.LimitingFactor);

            if (!string.IsNullOrEmpty(region.Description))
            {
                AppendLine(string.Empty);
                AppendLine(region.Description);
            }

            AppendLine(string.Empty);
            AppendLine("SETTLEMENT ANCHOR // RESERVED FOR PHASE 04");

            return _builder.ToString();
        }

        private void AppendLine(string line)
        {
            _builder.Append(line);
            _builder.Append('\n');
        }

        private void OnFocusClicked()
        {
            if (FocusRequested != null)
            {
                FocusRequested();
            }
        }

        private void OnBackClicked()
        {
            if (OverviewRequested != null)
            {
                OverviewRequested();
            }
        }
    }
}

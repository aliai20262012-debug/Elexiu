using System;
using System.Collections.Generic;
using System.Globalization;
using Nexus.Configuration;
using Nexus.Core.Platform;
using Nexus.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace Nexus.UI
{
    /// <summary>
    /// Bottom simulation control surface: clock readout, lifecycle status, pause/resume and
    /// the configured speed ladder. Presentation only - it raises intent events and never
    /// touches the clock or the session directly, so it stays unit testable and reusable on
    /// desktop, tablet and phone layouts.
    /// </summary>
    public sealed class SimulationControlBar
    {
        private readonly UiTheme _theme;
        private readonly List<UiButton> _speedButtons = new List<UiButton>();
        private readonly List<double> _speeds = new List<double>();
        private readonly UiButton _pauseResumeButton;
        private readonly UiLabel _timeLabel;
        private readonly UiLabel _statusLabel;
        private readonly UiLabel _sessionLabel;
        private readonly Image _background;
        private readonly float _buttonHeight;

        private string _sessionDescription = "NO SESSION";
        private long _lastTick;
        private double _speed = SimulationSpeed.Normal;
        private SimulationStatus _status = SimulationStatus.Stopped;

        /// <summary>Raised when the player presses pause/resume.</summary>
        public event Action PauseResumeRequested;

        /// <summary>Raised with the requested multiplier when a speed button is pressed.</summary>
        public event Action<double> SpeedRequested;

        /// <summary>Right aligned row that hosts the control buttons (the HUD appends "menu").</summary>
        public RectTransform ActionRow { get; }

        public float BarHeight { get; }

        public UiButton PauseResumeButton
        {
            get { return _pauseResumeButton; }
        }

        /// <summary>Rendered clock readout; exposed for automated tests and diagnostics.</summary>
        public string ClockText
        {
            get { return _timeLabel != null ? _timeLabel.Text : string.Empty; }
        }

        /// <summary>Rendered lifecycle line ("RUNNING // 2x", ...).</summary>
        public string StatusText
        {
            get { return _statusLabel != null ? _statusLabel.Text : string.Empty; }
        }

        /// <summary>Rendered session line ("AURORA // SEED 42 // TICK 000007").</summary>
        public string SessionText
        {
            get { return _sessionLabel != null ? _sessionLabel.Text : string.Empty; }
        }

        public IReadOnlyList<UiButton> SpeedButtons
        {
            get { return _speedButtons; }
        }

        public SimulationControlBar(
            RectTransform parent,
            UiTheme theme,
            GameConfiguration configuration,
            IPlatformInfo platform)
        {
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            if (theme == null)
            {
                throw new ArgumentNullException(nameof(theme));
            }

            _theme = theme;

            UiLayoutProfile layout = platform != null ? platform.Layout : UiLayoutProfile.Regular;
            bool compact = layout == UiLayoutProfile.Compact;
            bool expanded = layout == UiLayoutProfile.Expanded;

            BarHeight = compact ? 156f : (expanded ? 148f : 144f);
            _buttonHeight = compact ? 52f : 60f;

            _background = UiFactory.CreateImage(parent, "SimulationBar", theme.PanelNavy);
            UiFactory.Stretch(
                _background.rectTransform,
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                Vector2.zero,
                new Vector2(0f, BarHeight));

            Image accent = UiFactory.CreateImage(parent, "SimulationBarAccent", WithAlpha(theme.PrimaryCyan, 0.45f));
            UiFactory.Stretch(
                accent.rectTransform,
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, BarHeight - 2f),
                new Vector2(0f, BarHeight));

            RectTransform info = UiFactory.CreateRect(parent, "ClockInfo");
            UiFactory.Stretch(
                info,
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(compact ? 26f : 36f, 18f),
                new Vector2(compact ? 460f : 560f, BarHeight - 18f));

            _timeLabel = UiFactory.CreateLabel(
                info,
                "SimulationTime",
                FormatClock(0.0),
                compact ? 26f : 30f,
                theme.PrimaryCyan,
                UiTextAlignment.Left,
                2f);
            UiFactory.Place(
                _timeLabel.RectTransform,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(compact ? 420f : 520f, 34f),
                new Vector2(0f, -4f));

            _statusLabel = UiFactory.CreateLabel(
                info,
                "SimulationStatus",
                DescribeStatus(SimulationStatus.Stopped, SimulationSpeed.Normal),
                compact ? 15f : 16f,
                theme.AccentGreen,
                UiTextAlignment.Left,
                3f);
            UiFactory.Place(
                _statusLabel.RectTransform,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(compact ? 420f : 520f, 22f),
                new Vector2(0f, -42f));

            _sessionLabel = UiFactory.CreateLabel(
                info,
                "SimulationSession",
                "NO SESSION",
                compact ? 13f : 14f,
                theme.TextMuted,
                UiTextAlignment.Left,
                2f);
            UiFactory.Place(
                _sessionLabel.RectTransform,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(compact ? 420f : 520f, 20f),
                new Vector2(0f, -68f));

            RectTransform row = UiFactory.CreateRect(parent, "SimulationActions");
            UiFactory.Stretch(
                row,
                new Vector2(0f, 0f),
                new Vector2(1f, 1f),
                new Vector2(compact ? 480f : 600f, 16f),
                new Vector2(-24f, -16f));

            HorizontalLayoutGroup group = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            group.spacing = compact ? 8f : 10f;
            group.childAlignment = TextAnchor.MiddleRight;
            group.childControlHeight = true;
            group.childControlWidth = true;
            group.childForceExpandHeight = false;
            group.childForceExpandWidth = false;
            group.padding = new RectOffset(0, 0, 0, 0);

            ActionRow = row;

            _pauseResumeButton = UiFactory.CreateButton(
                row,
                "PauseResumeButton",
                "PAUSE",
                theme);
            ApplyButtonMetrics(_pauseResumeButton, compact ? 132f : 150f, _buttonHeight);
            _pauseResumeButton.Label.SetFontSize(compact ? 16f : 18f);
            _pauseResumeButton.Label.SetColor(theme.AccentGreen);
            _pauseResumeButton.Button.onClick.AddListener(OnPauseResumeClicked);

            IReadOnlyList<float> configured = configuration != null
                ? configuration.SimulationSpeedMultipliers
                : new[] { 1f, 2f, 4f };

            for (int i = 0; i < configured.Count; i++)
            {
                double level = configured[i];
                _speeds.Add(level);

                UiButton speedButton = UiFactory.CreateButton(
                    row,
                    "Speed" + i,
                    SimulationSpeed.FormatLabel(level),
                    theme);
                ApplyButtonMetrics(speedButton, compact ? 74f : 86f, _buttonHeight);
                speedButton.Label.SetFontSize(compact ? 16f : 18f);

                double captured = level;
                speedButton.Button.onClick.AddListener(() => OnSpeedClicked(captured));
                _speedButtons.Add(speedButton);
            }

            RefreshSpeedButtons();
            RefreshPauseButton();
        }

        /// <summary>Requests a specific speed from the owning controller.</summary>
        public void RequestSpeed(double multiplier)
        {
            Action<double> handler = SpeedRequested;
            if (handler != null)
            {
                handler(multiplier);
            }
        }

        /// <summary>Requests a pause/resume toggle from the owning controller.</summary>
        public void RequestPauseResume()
        {
            Action handler = PauseResumeRequested;
            if (handler != null)
            {
                handler();
            }
        }

        public void SetStatus(SimulationStatus status, double speed)
        {
            _status = status;
            _speed = SimulationSpeed.Clamp(speed);

            if (_statusLabel != null)
            {
                _statusLabel.Text = DescribeStatus(_status, _speed);
                _statusLabel.SetColor(StatusColor(_status));
            }

            RefreshPauseButton();
            RefreshSpeedButtons();
        }

        /// <summary>Shows a transient message (errors, scene transitions) instead of the state text.</summary>
        public void SetMessage(string message)
        {
            if (_statusLabel == null)
            {
                return;
            }

            _statusLabel.Text = string.IsNullOrEmpty(message) ? string.Empty : message.ToUpperInvariant();
            _statusLabel.SetColor(_theme.TextMuted);
        }

        public void SetTime(double elapsedSeconds, long tick)
        {
            _lastTick = tick < 0 ? 0 : tick;

            if (_timeLabel != null)
            {
                _timeLabel.Text = FormatClock(elapsedSeconds);
            }

            RefreshSessionLabel();
        }

        /// <summary>Updates the tick counter without touching the clock readout.</summary>
        public void SetTick(long tick)
        {
            _lastTick = tick < 0 ? 0 : tick;
            RefreshSessionLabel();
        }

        public void SetSession(GameSession session)
        {
            _sessionDescription = session == null
                ? "NO SESSION"
                : session.CivilizationName.ToUpperInvariant() +
                  " // SEED " + session.Seed.ToString(CultureInfo.InvariantCulture);

            RefreshSessionLabel();
        }

        private void RefreshSessionLabel()
        {
            if (_sessionLabel == null)
            {
                return;
            }

            _sessionLabel.Text = _sessionDescription +
                " // TICK " + _lastTick.ToString("000000", CultureInfo.InvariantCulture);
        }

        /// <summary>Formats simulated seconds as T+ HH:MM:SS (hours are not clamped to a day).</summary>
        public static string FormatClock(double elapsedSeconds)
        {
            double safe = elapsedSeconds;
            if (double.IsNaN(safe) || double.IsInfinity(safe) || safe < 0.0)
            {
                safe = 0.0;
            }

            long total = (long)Math.Floor(safe);
            long hours = total / 3600;
            long minutes = (total % 3600) / 60;
            long seconds = total % 60;

            return string.Format(CultureInfo.InvariantCulture, "T+ {0:00}:{1:00}:{2:00}", hours, minutes, seconds);
        }

        /// <summary>"RUNNING // 2x", "PAUSED // 1x", ... used by the HUD and by tests.</summary>
        public static string DescribeStatus(SimulationStatus status, double speed)
        {
            switch (status)
            {
                case SimulationStatus.Running:
                    return "RUNNING // " + SimulationSpeed.FormatLabel(speed);

                case SimulationStatus.Paused:
                    return "PAUSED // " + SimulationSpeed.FormatLabel(speed);

                case SimulationStatus.Failed:
                    return "INITIALIZATION FAILED";

                default:
                    return "STOPPED";
            }
        }

        private Color StatusColor(SimulationStatus status)
        {
            switch (status)
            {
                case SimulationStatus.Running:
                    return _theme.AccentGreen;

                case SimulationStatus.Paused:
                    return _theme.PrimaryCyan;

                case SimulationStatus.Failed:
                    return _theme.Danger;

                default:
                    return _theme.TextMuted;
            }
        }

        private void OnPauseResumeClicked()
        {
            RequestPauseResume();
        }

        private void OnSpeedClicked(double multiplier)
        {
            RequestSpeed(multiplier);
        }

        private void RefreshPauseButton()
        {
            if (_pauseResumeButton == null)
            {
                return;
            }

            bool paused = _status == SimulationStatus.Paused;
            _pauseResumeButton.Label.Text = paused ? "RESUME" : "PAUSE";
            _pauseResumeButton.Interactable = _status == SimulationStatus.Running || _status == SimulationStatus.Paused;
        }

        private void RefreshSpeedButtons()
        {
            for (int i = 0; i < _speedButtons.Count; i++)
            {
                bool selected = Math.Abs(_speeds[i] - _speed) < 0.0001;
                UiButton button = _speedButtons[i];

                if (button.Background != null)
                {
                    button.Background.color = selected
                        ? Color.Lerp(_theme.SurfaceNavy, _theme.PrimaryCyan, 0.30f)
                        : _theme.SurfaceNavy;
                }

                button.Label.SetColor(selected ? _theme.PrimaryCyan : _theme.TextPrimary);
                button.Interactable = _status == SimulationStatus.Running || _status == SimulationStatus.Paused;
            }
        }

        private static void ApplyButtonMetrics(UiButton button, float width, float height)
        {
            if (button == null || button.RectTransform == null)
            {
                return;
            }

            LayoutElement layout = button.RectTransform.GetComponent<LayoutElement>();
            if (layout == null)
            {
                layout = button.RectTransform.gameObject.AddComponent<LayoutElement>();
            }

            layout.preferredWidth = width;
            layout.minWidth = width;
            layout.flexibleWidth = 0f;
            layout.preferredHeight = height;
            layout.minHeight = height;
            layout.flexibleHeight = 0f;
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            return new Color(color.r, color.g, color.b, alpha);
        }
    }
}

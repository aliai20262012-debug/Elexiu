using System.Collections.Generic;
using Nexus.Core.Logging;
using Nexus.Core.Scenes;
using UnityEngine;

namespace Nexus.Configuration
{
    /// <summary>
    /// Immutable-by-convention project configuration. Designers tune the asset in
    /// Resources/Config; code falls back to validated defaults when it is missing,
    /// so the application never hard fails because of a missing asset.
    /// </summary>
    [CreateAssetMenu(menuName = "Nexus/Configuration/Game Configuration", fileName = "GameConfiguration")]
    public sealed class GameConfiguration : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string _gameTitle = "NEXUS CIVILIZATION";
        [SerializeField] private string _buildVersion = "0.2.0-phase02";
        [SerializeField] private string _buildChannel = "CORE SIMULATION";

        [Header("Scenes")]
        [SerializeField] private string _bootstrapScene = SceneNames.Bootstrap;
        [SerializeField] private string _mainMenuScene = SceneNames.MainMenu;
        [SerializeField] private string _gameplayScene = SceneNames.Gameplay;

        [Header("Performance")]
        [Tooltip("Frames per second target on desktop targets.")]
        [SerializeField] private int _desktopTargetFrameRate = 120;

        [Tooltip("Frames per second target on mobile targets.")]
        [SerializeField] private int _mobileTargetFrameRate = 60;

        [Tooltip("Seconds between simulation ticks while gameplay is running.")]
        [SerializeField] private float _simulationTickIntervalSeconds = 0.5f;

        [Header("Simulation")]
        [Tooltip("Selectable speed multipliers. Invalid, duplicate and out of range values are discarded at load time.")]
        [SerializeField] private float[] _simulationSpeedMultipliers = { 1f, 2f, 4f };

        private float[] _resolvedSpeedMultipliers = { 1f, 2f, 4f };

        [Tooltip("Speed used when a session starts. Snapped to the nearest configured level.")]
        [SerializeField] private float _defaultSimulationSpeedMultiplier = 1f;

        [Tooltip("Start new sessions paused instead of running.")]
        [SerializeField] private bool _simulationStartsPaused;

        [Tooltip("Maximum number of ticks a single frame may catch up on. Backlog beyond this is discarded.")]
        [SerializeField] private int _maxCatchUpTicksPerFrame = 8;

        [Tooltip("Frame deltas are clamped to this value (seconds) so a stall cannot fast-forward the world.")]
        [SerializeField] private float _maxFrameDeltaSeconds = 0.25f;

        [Tooltip("Fallback civilization name used when a new session is created without a player supplied name.")]
        [SerializeField] private string _defaultCivilizationName = "NEW COLONY";

        [Header("Diagnostics")]
        [SerializeField] private bool _enableVerboseLogging;

        public string GameTitle { get { return string.IsNullOrEmpty(_gameTitle) ? "NEXUS CIVILIZATION" : _gameTitle; } }

        public string BuildVersion { get { return string.IsNullOrEmpty(_buildVersion) ? "0.0.0" : _buildVersion; } }

        public string BuildChannel { get { return string.IsNullOrEmpty(_buildChannel) ? "DEVELOPMENT" : _buildChannel; } }

        public string BootstrapScene { get { return string.IsNullOrEmpty(_bootstrapScene) ? SceneNames.Bootstrap : _bootstrapScene; } }

        public string MainMenuScene { get { return string.IsNullOrEmpty(_mainMenuScene) ? SceneNames.MainMenu : _mainMenuScene; } }

        public string GameplayScene { get { return string.IsNullOrEmpty(_gameplayScene) ? SceneNames.Gameplay : _gameplayScene; } }

        public int DesktopTargetFrameRate { get { return Mathf.Clamp(_desktopTargetFrameRate, 30, 360); } }

        public int MobileTargetFrameRate { get { return Mathf.Clamp(_mobileTargetFrameRate, 30, 120); } }

        public float SimulationTickIntervalSeconds { get { return Mathf.Clamp(_simulationTickIntervalSeconds, 0.05f, 5f); } }

        /// <summary>Validated speed ladder (ascending, de-duplicated, never empty).</summary>
        public IReadOnlyList<float> SimulationSpeedMultipliers { get { return _resolvedSpeedMultipliers; } }

        public float DefaultSimulationSpeedMultiplier
        {
            get { return NearestConfiguredSpeed(_resolvedSpeedMultipliers, _defaultSimulationSpeedMultiplier); }
        }

        public bool SimulationStartsPaused { get { return _simulationStartsPaused; } }

        public int MaxCatchUpTicksPerFrame { get { return Mathf.Clamp(_maxCatchUpTicksPerFrame, 1, 64); } }

        public float MaxFrameDeltaSeconds { get { return Mathf.Clamp(_maxFrameDeltaSeconds, 0.02f, 1f); } }

        public string DefaultCivilizationName
        {
            get
            {
                if (string.IsNullOrEmpty(_defaultCivilizationName) || _defaultCivilizationName.Trim().Length == 0)
                {
                    return "NEW COLONY";
                }

                string trimmed = _defaultCivilizationName.Trim();
                return trimmed.Length > 48 ? trimmed.Substring(0, 48).Trim() : trimmed;
            }
        }

        public bool EnableVerboseLogging { get { return _enableVerboseLogging; } }

        public int TargetFrameRateFor(bool isMobile)
        {
            return isMobile ? MobileTargetFrameRate : DesktopTargetFrameRate;
        }

        public static GameConfiguration CreateDefault()
        {
            GameConfiguration configuration = CreateInstance<GameConfiguration>();
            configuration.name = "GameConfiguration (runtime default)";
            configuration.hideFlags = HideFlags.DontSave;
            configuration.Sanitize();
            return configuration;
        }

        private void OnEnable()
        {
            Sanitize();
        }

        private void OnValidate()
        {
            Sanitize();
        }

        private void Sanitize()
        {
            _desktopTargetFrameRate = Mathf.Clamp(_desktopTargetFrameRate, 30, 360);
            _mobileTargetFrameRate = Mathf.Clamp(_mobileTargetFrameRate, 30, 120);
            _simulationTickIntervalSeconds = Mathf.Clamp(_simulationTickIntervalSeconds, 0.05f, 5f);
            _resolvedSpeedMultipliers = SanitizeSpeedMultipliers(_simulationSpeedMultipliers);
            _maxCatchUpTicksPerFrame = Mathf.Clamp(_maxCatchUpTicksPerFrame, 1, 64);
            _maxFrameDeltaSeconds = Mathf.Clamp(_maxFrameDeltaSeconds, 0.02f, 1f);
            _defaultSimulationSpeedMultiplier = NearestConfiguredSpeed(_resolvedSpeedMultipliers, _defaultSimulationSpeedMultiplier);

            if (string.IsNullOrEmpty(_bootstrapScene) && string.IsNullOrEmpty(_mainMenuScene))
            {
                GameLog.Warning("GameConfiguration has no scenes configured; falling back to built-in defaults.", this);
                _bootstrapScene = SceneNames.Bootstrap;
                _mainMenuScene = SceneNames.MainMenu;
                _gameplayScene = SceneNames.Gameplay;
            }
        }

        /// <summary>
        /// Filters non-finite, out of range and duplicate speed multipliers and sorts the
        /// result. Falls back to the 1x / 2x / 4x ladder, so the simulation always has a
        /// usable speed control even if the asset is edited badly.
        /// </summary>
        public static float[] SanitizeSpeedMultipliers(float[] values)
        {
            if (values == null || values.Length == 0)
            {
                return new[] { 1f, 2f, 4f };
            }

            List<float> valid = new List<float>(values.Length);
            for (int i = 0; i < values.Length; i++)
            {
                float value = values[i];
                if (float.IsNaN(value) || float.IsInfinity(value))
                {
                    continue;
                }

                if (value < 0.25f || value > 100f)
                {
                    continue;
                }

                valid.Add(value);
            }

            if (valid.Count == 0)
            {
                return new[] { 1f, 2f, 4f };
            }

            valid.Sort();

            List<float> unique = new List<float>(valid.Count);
            for (int i = 0; i < valid.Count; i++)
            {
                if (unique.Count == 0 || Mathf.Abs(unique[unique.Count - 1] - valid[i]) > 0.0001f)
                {
                    unique.Add(valid[i]);
                }
            }

            return unique.ToArray();
        }

        /// <summary>Returns the configured level closest to the requested multiplier.</summary>
        public static float NearestConfiguredSpeed(IReadOnlyList<float> levels, float preferred)
        {
            if (levels == null || levels.Count == 0)
            {
                return 1f;
            }

            float target = float.IsNaN(preferred) ? 1f : Mathf.Clamp(preferred, 0.25f, 100f);
            float best = levels[0];
            float bestDelta = Mathf.Abs(levels[0] - target);

            for (int i = 1; i < levels.Count; i++)
            {
                float delta = Mathf.Abs(levels[i] - target);
                if (delta < bestDelta)
                {
                    best = levels[i];
                    bestDelta = delta;
                }
            }

            return best;
        }
    }
}

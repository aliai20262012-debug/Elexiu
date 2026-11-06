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
        [SerializeField] private string _buildVersion = "0.1.0-phase01";
        [SerializeField] private string _buildChannel = "FOUNDATION";

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

            if (string.IsNullOrEmpty(_bootstrapScene) && string.IsNullOrEmpty(_mainMenuScene))
            {
                GameLog.Warning("GameConfiguration has no scenes configured; falling back to built-in defaults.", this);
                _bootstrapScene = SceneNames.Bootstrap;
                _mainMenuScene = SceneNames.MainMenu;
                _gameplayScene = SceneNames.Gameplay;
            }
        }
    }
}

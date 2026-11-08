using System.Collections;
using Nexus.Configuration;
using Nexus.Core.Logging;
using Nexus.Core.Scenes;
using Nexus.Core.State;
using Nexus.UI;
using UnityEngine;

namespace Nexus.Core.Bootstrap
{
    /// <summary>
    /// Entry point of the Bootstrap scene. Shows a loading surface, composes the application,
    /// then hands control to the main menu. Every failure is surfaced to the player and can be
    /// retried without restarting the executable.
    /// </summary>
    public sealed class BootstrapController : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Minimum time the splash stays visible so the transition does not flash.")]
        private float _minimumSplashSeconds = 0.5f;

        [SerializeField]
        [Tooltip("Maximum time to wait for the main menu scene before reporting a failure.")]
        private float _mainMenuLoadTimeoutSeconds = 20f;

        private BootstrapLoadingView _loadingView;
        private ErrorOverlay _errorOverlay;

        public bool IsComplete { get; private set; }

        private void Start()
        {
            StartCoroutine(RunBootSequence());
        }

        private IEnumerator RunBootSequence()
        {
            IsComplete = false;

            IConfigurationService previewConfiguration = ConfigurationService.LoadOrDefaults();
            _loadingView = new BootstrapLoadingView(previewConfiguration.Theme);
            UiFactory.EnsureEventSystem();

            yield return null;

            _loadingView.SetProgress(0.25f);
            _loadingView.SetStatus("COMPOSING SERVICES");

            BootResult bootResult = ApplicationBootstrapper.Boot();
            if (!bootResult.Succeeded)
            {
                ShowFailure("BOOT FAILURE", bootResult.Error);
                yield break;
            }

            _loadingView.SetProgress(0.65f);
            _loadingView.SetStatus("LOADING COMMAND INTERFACE");

            float elapsed = 0f;
            while (elapsed < Mathf.Max(0f, _minimumSplashSeconds))
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            GameManager gameManager = AppContext.Get<GameManager>();

            bool finished = false;
            SceneLoadResult result = default(SceneLoadResult);
            System.Action<SceneLoadResult> onMenuSceneReady = loadResult =>
            {
                result = loadResult;
                finished = true;
            };

            if (gameManager.State == GameState.MainMenu)
            {
                // The bootstrap scene was re-entered while the application was already running:
                // navigate through the scene loader instead of replaying the boot sequence.
                ISceneLoader sceneLoader;
                IConfigurationService configuration;
                if (AppContext.TryGet(out sceneLoader) && AppContext.TryGet(out configuration))
                {
                    sceneLoader.Load(configuration.Game.MainMenuScene, onMenuSceneReady);
                }
                else
                {
                    ShowFailure("NAVIGATION FAILURE", "The scene loader or configuration service is missing from the application context.");
                    yield break;
                }
            }
            else
            {
                gameManager.EnterMainMenu(onMenuSceneReady);
            }

            float timeout = Mathf.Max(1f, _mainMenuLoadTimeoutSeconds);
            while (!finished && timeout > 0f)
            {
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }

            if (!finished)
            {
                ShowFailure("SCENE LOAD TIMEOUT", "The main menu scene did not finish loading within " + _mainMenuLoadTimeoutSeconds + " seconds.");
                yield break;
            }

            if (!result.Succeeded)
            {
                ShowFailure("SCENE LOAD FAILURE", result.Error);
                yield break;
            }

            IsComplete = true;
            _loadingView.SetProgress(1f);
        }

        private void ShowFailure(string title, string message)
        {
            GameLog.Error(title + ": " + message);

            if (_loadingView != null)
            {
                _loadingView.SetStatus("FAILED");
            }

            IConfigurationService configuration = null;
            AppContext.TryGet(out configuration);
            UiTheme theme = configuration != null ? configuration.Theme : UiTheme.CreateDefault();

            _errorOverlay = ErrorOverlay.Show(title, message, Retry, theme);
        }

        private void Retry()
        {
            if (_errorOverlay != null)
            {
                Destroy(_errorOverlay.gameObject);
                _errorOverlay = null;
            }

            AppContext.Uninstall();
            StopAllCoroutines();
            StartCoroutine(RunBootSequence());
        }
    }
}

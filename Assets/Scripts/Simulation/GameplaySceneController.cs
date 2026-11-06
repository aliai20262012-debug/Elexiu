using Nexus.CameraSystem;
using Nexus.Configuration;
using Nexus.Core;
using Nexus.Core.Bootstrap;
using Nexus.Core.Events;
using Nexus.Core.Logging;
using Nexus.Core.Platform;
using Nexus.Core.State;
using Nexus.Planet;
using Nexus.UI;
using UnityEngine;

namespace Nexus.Simulation
{
    /// <summary>
    /// Gameplay scene shell: starts the simulation clock, builds the placeholder planet and
    /// HUD, and routes "return to menu" through the GameManager. Gameplay rules are absent by
    /// design in Phase 01; domain systems subscribe to SimulationTickEvent later.
    /// </summary>
    public sealed class GameplaySceneController : MonoBehaviour
    {
        private GameManager _gameManager;
        private ISimulationManager _simulation;
        private IEventBus _eventBus;
        private IConfigurationService _configuration;
        private IPlatformInfo _platform;
        private GameplayHud _hud;
        private PlanetViewController _planet;

        public GameplayHud Hud
        {
            get { return _hud; }
        }

        public long TickCount
        {
            get { return _simulation != null ? _simulation.TickCount : 0L; }
        }

        private void Start()
        {
            if (!TryResolveServices())
            {
                return;
            }

            BuildSceneShell();

            _eventBus.Subscribe<SimulationTickEvent>(HandleSimulationTick);
            _simulation.StartSimulation();
        }

        private void Update()
        {
            if (_simulation != null)
            {
                _simulation.Tick(Time.deltaTime);
            }
        }

        private void OnDestroy()
        {
            if (_eventBus != null)
            {
                _eventBus.Unsubscribe<SimulationTickEvent>(HandleSimulationTick);
                _eventBus = null;
            }

            if (_simulation != null)
            {
                _simulation.Stop();
            }
        }

        private bool TryResolveServices()
        {
            if (!AppContext.IsReady)
            {
                GameLog.Warning("Gameplay started without the bootstrap scene; composing services locally.");
                BootResult bootResult = ApplicationBootstrapper.Boot();
                if (!bootResult.Succeeded)
                {
                    ErrorOverlay.Show("BOOT FAILURE", bootResult.Error, null, UiTheme.CreateDefault());
                    return false;
                }

                // The gameplay scene was opened directly: move the state machine to the
                // state it would have had after the normal main-menu flow.
                AppContext.Get<GameManager>().StateMachine.TryTransitionTo(GameState.MainMenu);
            }

            bool resolved =
                AppContext.TryGet(out _gameManager) &&
                AppContext.TryGet(out _simulation) &&
                AppContext.TryGet(out _eventBus) &&
                AppContext.TryGet(out _configuration) &&
                AppContext.TryGet(out _platform);

            if (!resolved)
            {
                ErrorOverlay.Show(
                    "SERVICE RESOLUTION FAILURE",
                    "The gameplay scene could not resolve its core services.",
                    null,
                    _configuration != null ? _configuration.Theme : UiTheme.CreateDefault());
                return false;
            }

            if (_gameManager.State == GameState.MainMenu)
            {
                _gameManager.StateMachine.TryTransitionTo(GameState.LoadingGameplay);
            }

            _gameManager.StateMachine.TryTransitionTo(GameState.Gameplay);
            return true;
        }

        private void BuildSceneShell()
        {
            UiFactory.EnsureEventSystem();

            _planet = Object.FindFirstObjectByType<PlanetViewController>();
            if (_planet == null)
            {
                _planet = gameObject.AddComponent<PlanetViewController>();
            }

            Camera sceneCamera = Camera.main;
            if (sceneCamera == null)
            {
                GameObject cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                sceneCamera = cameraObject.AddComponent<Camera>();
                sceneCamera.backgroundColor = _configuration.Theme.BackgroundDeepNavy;
                sceneCamera.clearFlags = CameraClearFlags.SolidColor;
            }
            else
            {
                sceneCamera.clearFlags = CameraClearFlags.SolidColor;
                sceneCamera.backgroundColor = _configuration.Theme.BackgroundDeepNavy;
            }

            CameraRig rig = sceneCamera.GetComponent<CameraRig>();
            if (rig == null)
            {
                rig = sceneCamera.gameObject.AddComponent<CameraRig>();
            }

            rig.Initialize(_planet.transform);

            _hud = new GameplayHud(_configuration.Theme, _configuration.Game, _platform);
            _hud.ReturnToMenuButton.Button.onClick.AddListener(OnReturnToMenuPressed);
        }

        private void HandleSimulationTick(SimulationTickEvent gameEvent)
        {
            if (_hud != null)
            {
                _hud.SetTick(gameEvent.Tick);
            }
        }

        private void OnReturnToMenuPressed()
        {
            if (_hud != null)
            {
                _hud.SetStatus("RETURNING TO COMMAND INTERFACE...");
            }

            _simulation.Stop();

            if (_gameManager != null)
            {
                _gameManager.ReturnToMainMenu(null);
            }
        }
    }
}

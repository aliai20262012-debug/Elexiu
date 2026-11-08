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
    /// Gameplay scene shell and the only place where frame time enters the simulation.
    ///
    /// Responsibilities:
    /// * resolve the composed services and keep the application state machine in sync,
    /// * create the game session through <see cref="IGameSessionService"/> (never directly),
    /// * feed <c>Time.deltaTime</c> into the deterministic driver once per frame,
    /// * translate HUD intents (pause, speed, menu) into session/simulation calls,
    /// * terminate the session when the scene is torn down or the player leaves.
    ///
    /// No gameplay rule lives here; domain systems register with the simulation manager.
    /// </summary>
    public sealed class GameplaySceneController : MonoBehaviour
    {
        private GameManager _gameManager;
        private ISimulationManager _simulation;
        private IGameSessionService _sessions;
        private IEventBus _eventBus;
        private IConfigurationService _configuration;
        private IPlatformInfo _platform;
        private GameplayHud _hud;
        private PlanetViewController _planet;
        private PlanetSceneController _planetScene;
        private Camera _sceneCamera;
        private ErrorOverlay _errorOverlay;
        private bool _subscribed;

        public GameplayHud Hud
        {
            get { return _hud; }
        }

        public IGameSessionService Sessions
        {
            get { return _sessions; }
        }

        public GameSession Session
        {
            get { return _sessions != null ? _sessions.Current : null; }
        }

        public long TickCount
        {
            get { return _simulation != null ? _simulation.TickCount : 0L; }
        }

        public double ElapsedSeconds
        {
            get { return _simulation != null ? _simulation.ElapsedSeconds : 0.0; }
        }

        /// <summary>Planet presentation controller (null until the session is running).</summary>
        public PlanetSceneController PlanetScene
        {
            get { return _planetScene; }
        }

        public PlanetViewController Planet
        {
            get { return _planet; }
        }

        public bool IsSimulationRunning
        {
            get { return _simulation != null && _simulation.Status == SimulationStatus.Running; }
        }

        public bool IsSimulationPaused
        {
            get { return _simulation != null && _simulation.Status == SimulationStatus.Paused; }
        }

        private void Start()
        {
            if (!TryResolveServices())
            {
                return;
            }

            BuildSceneShell();
            SubscribeToSimulation();

            if (!StartSession())
            {
                return;
            }

            BuildPlanetAndInteraction();
            RefreshHudFromSimulation();
        }

        private void Update()
        {
            if (_simulation == null || !_simulation.IsRunning)
            {
                return;
            }

            // The single frame-time boundary of the simulation. Everything below works in
            // whole ticks, so changing the frame rate can never change the number of ticks.
            _simulation.Tick(Time.deltaTime);
        }

        private void OnDestroy()
        {
            try
            {
                UnsubscribeFromSimulation();
                ReleaseHudSubscriptions();

                if (_sessions != null && _sessions.HasActiveSession)
                {
                    _sessions.EndSession(SessionEndReason.SceneUnloaded);
                }
                else if (_simulation != null && _simulation.IsRunning)
                {
                    _simulation.Stop();
                }
            }
            catch (System.Exception exception)
            {
                // Scene teardown during application shutdown must never throw.
                GameLog.Exception(exception, "Gameplay scene teardown reported an error.");
            }

            _eventBus = null;
        }

        /// <summary>Pauses or resumes the session and mirrors the change in the app state machine.</summary>
        public bool ToggleSimulationPause()
        {
            if (_sessions == null || !_sessions.HasActiveSession)
            {
                return false;
            }

            GameSession session = _sessions.Current;
            if (session == null)
            {
                return false;
            }

            if (session.State == GameSessionState.Paused)
            {
                if (!_sessions.Resume())
                {
                    return false;
                }

                if (_gameManager != null && _gameManager.State == GameState.Paused)
                {
                    _gameManager.Resume();
                }

                return true;
            }

            if (session.State != GameSessionState.Active)
            {
                return false;
            }

            if (!_sessions.Pause())
            {
                return false;
            }

            if (_gameManager != null && _gameManager.State == GameState.Gameplay)
            {
                _gameManager.Pause();
            }

            return true;
        }

        /// <summary>Requests a speed level for the active session.</summary>
        public bool SelectSpeed(double multiplier)
        {
            if (_sessions == null || !_sessions.HasActiveSession)
            {
                return false;
            }

            return _sessions.SetSimulationSpeed(multiplier);
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
                AppContext.TryGet(out _sessions) &&
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

            // Camera framing and input belong to the planet scene controller, which configures
            // the orbit camera once the planet exists (see BuildPlanetAndInteraction).
            _sceneCamera = sceneCamera;

            _hud = new GameplayHud(_configuration.Theme, _configuration.Game, _platform);
            _hud.ReturnToMenuButton.Button.onClick.AddListener(OnReturnToMenuPressed);
            _hud.ControlBar.PauseResumeRequested += OnPauseResumeRequested;
            _hud.ControlBar.SpeedRequested += OnSpeedRequested;
        }

        /// <summary>
        /// Builds the procedural planet with the session seed and wires input, camera and the
        /// region panel. Runs after the session exists so the planet is deterministic per seed.
        /// </summary>
        private void BuildPlanetAndInteraction()
        {
            if (_planet == null || _sceneCamera == null)
            {
                return;
            }

            GameSession session = _sessions != null ? _sessions.Current : null;
            int seed = session != null ? session.Seed : 0;
            bool highQuality = _platform == null || !_platform.IsMobile || _platform.IsTablet;

            _planet.Build(seed, highQuality);

            _planetScene = GetComponent<PlanetSceneController>();
            if (_planetScene == null)
            {
                _planetScene = gameObject.AddComponent<PlanetSceneController>();
            }

            _planetScene.Initialize(_planet, _hud, _sceneCamera, _eventBus, _platform);

            if (_planet.Definition != null)
            {
                GameLog.Info(
                    "Planet " + _planet.Definition.DisplayName + " generated with " +
                    _planet.Regions.Count + " regions (seed " + seed + ").");
            }
        }

        private bool StartSession()
        {
            if (_sessions.HasActiveSession)
            {
                // Re-entering the scene while a session survived (for example after a
                // failed reload) reuses it instead of orphaning it.
                _hud.SetSession(_sessions.Current);
                return true;
            }

            GameSession session;
            string error;
            if (!_sessions.TryCreateSession(out session, out error))
            {
                GameLog.Error("The simulation session could not be started: " + error);
                _hud.SetStatus("SIMULATION START FAILED");
                _errorOverlay = ErrorOverlay.Show(
                    "SIMULATION START FAILURE",
                    error,
                    RetryStartSession,
                    _configuration.Theme);
                return false;
            }

            _hud.SetSession(session);
            GameLog.Info("Game session '" + session.SessionId + "' created for " + session.CivilizationName + " (seed " + session.Seed + ").");
            return true;
        }

        private void RetryStartSession()
        {
            if (_errorOverlay != null)
            {
                Destroy(_errorOverlay.gameObject);
                _errorOverlay = null;
            }

            if (StartSession())
            {
                RefreshHudFromSimulation();
            }
        }

        private void SubscribeToSimulation()
        {
            if (_eventBus == null || _subscribed)
            {
                return;
            }

            _eventBus.Subscribe<SimulationTickCompletedEvent>(HandleTickCompleted);
            _eventBus.Subscribe<SimulationStartedEvent>(HandleSimulationStarted);
            _eventBus.Subscribe<SimulationPausedEvent>(HandleSimulationPaused);
            _eventBus.Subscribe<SimulationResumedEvent>(HandleSimulationResumed);
            _eventBus.Subscribe<SimulationSpeedChangedEvent>(HandleSpeedChanged);
            _eventBus.Subscribe<SimulationStoppedEvent>(HandleSimulationStopped);
            _subscribed = true;
        }

        private void UnsubscribeFromSimulation()
        {
            if (_eventBus == null || !_subscribed)
            {
                return;
            }

            _eventBus.Unsubscribe<SimulationTickCompletedEvent>(HandleTickCompleted);
            _eventBus.Unsubscribe<SimulationStartedEvent>(HandleSimulationStarted);
            _eventBus.Unsubscribe<SimulationPausedEvent>(HandleSimulationPaused);
            _eventBus.Unsubscribe<SimulationResumedEvent>(HandleSimulationResumed);
            _eventBus.Unsubscribe<SimulationSpeedChangedEvent>(HandleSpeedChanged);
            _eventBus.Unsubscribe<SimulationStoppedEvent>(HandleSimulationStopped);
            _subscribed = false;
        }

        private void ReleaseHudSubscriptions()
        {
            if (_hud == null)
            {
                return;
            }

            if (_hud.ReturnToMenuButton != null && _hud.ReturnToMenuButton.Button != null)
            {
                _hud.ReturnToMenuButton.Button.onClick.RemoveListener(OnReturnToMenuPressed);
            }

            if (_hud.ControlBar != null)
            {
                _hud.ControlBar.PauseResumeRequested -= OnPauseResumeRequested;
                _hud.ControlBar.SpeedRequested -= OnSpeedRequested;
            }
        }

        private void HandleTickCompleted(SimulationTickCompletedEvent gameEvent)
        {
            if (_hud == null)
            {
                return;
            }

            _hud.SetSimulationTime(gameEvent.ElapsedSeconds, gameEvent.Tick);
        }

        private void HandleSimulationStarted(SimulationStartedEvent gameEvent)
        {
            RefreshHudFromSimulation();
        }

        private void HandleSimulationPaused(SimulationPausedEvent gameEvent)
        {
            RefreshHudFromSimulation();
        }

        private void HandleSimulationResumed(SimulationResumedEvent gameEvent)
        {
            RefreshHudFromSimulation();
        }

        private void HandleSpeedChanged(SimulationSpeedChangedEvent gameEvent)
        {
            RefreshHudFromSimulation();
        }

        private void HandleSimulationStopped(SimulationStoppedEvent gameEvent)
        {
            RefreshHudFromSimulation();
        }

        private void RefreshHudFromSimulation()
        {
            if (_hud == null || _simulation == null)
            {
                return;
            }

            _hud.SetSimulationStatus(_simulation.Status, _simulation.SpeedMultiplier);
            _hud.SetSimulationTime(_simulation.ElapsedSeconds, _simulation.TickCount);
            _hud.SetSession(_sessions != null ? _sessions.Current : null);
        }

        private void OnPauseResumeRequested()
        {
            ToggleSimulationPause();
        }

        private void OnSpeedRequested(double multiplier)
        {
            SelectSpeed(multiplier);
        }

        private void OnReturnToMenuPressed()
        {
            if (_planetScene != null)
            {
                _planetScene.ReturnToOverview();
            }

            if (_sessions != null && _sessions.HasActiveSession)
            {
                _sessions.EndSession(SessionEndReason.PlayerRequested);
            }
            else if (_simulation != null)
            {
                _simulation.Stop();
            }

            // Set after the stop event so the transition message is not overwritten by the
            // "STOPPED" state refresh.
            if (_hud != null)
            {
                _hud.SetStatus("RETURNING TO COMMAND INTERFACE...");
            }

            if (_gameManager != null)
            {
                _gameManager.ReturnToMainMenu(null);
            }
        }
    }
}

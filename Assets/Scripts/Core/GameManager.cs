using System;
using Nexus.Configuration;
using Nexus.Core.Events;
using Nexus.Core.Logging;
using Nexus.Core.Scenes;
using Nexus.Core.State;
using Nexus.SaveSystem;

namespace Nexus.Core
{
    /// <summary>
    /// Thin application coordinator. It owns the state machine and the scene flow and
    /// nothing else: no rendering, no gameplay rules, no persistence logic.
    /// </summary>
    public sealed class GameManager : IDisposable
    {
        private readonly IEventBus _eventBus;
        private readonly ISceneLoader _sceneLoader;
        private readonly IConfigurationService _configuration;
        private readonly ISaveService _saveService;
        private readonly GameStateMachine _stateMachine = new GameStateMachine();
        private bool _disposed;

        public GameState State
        {
            get { return _stateMachine.CurrentState; }
        }

        public GameStateMachine StateMachine
        {
            get { return _stateMachine; }
        }

        /// <summary>True when a save exists, which is what enables the Continue button.</summary>
        public bool CanContinue
        {
            get { return _saveService != null && _saveService.HasAnySave; }
        }

        public GameManager(
            IEventBus eventBus,
            ISceneLoader sceneLoader,
            IConfigurationService configuration,
            ISaveService saveService)
        {
            if (eventBus == null)
            {
                throw new ArgumentNullException(nameof(eventBus));
            }

            if (sceneLoader == null)
            {
                throw new ArgumentNullException(nameof(sceneLoader));
            }

            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            _eventBus = eventBus;
            _sceneLoader = sceneLoader;
            _configuration = configuration;
            _saveService = saveService;
            _stateMachine.StateChanged += HandleStateChanged;
        }

        public void Initialize()
        {
            if (State != GameState.Uninitialized)
            {
                GameLog.Warning("GameManager.Initialize ignored: already in state " + State + ".");
                return;
            }

            _stateMachine.TransitionTo(GameState.Booting);
        }

        public void EnterMainMenu(Action<SceneLoadResult> onCompleted = null)
        {
            if (State == GameState.MainMenu)
            {
                Complete(onCompleted, SceneLoadResult.Success(_configuration.Game.MainMenuScene));
                return;
            }

            if (State != GameState.Booting && State != GameState.ReturningToMenu)
            {
                Fail(onCompleted, "Cannot enter the main menu from state " + State + ".");
                return;
            }

            LoadScene(_configuration.Game.MainMenuScene, result =>
            {
                if (!result.Succeeded)
                {
                    ReportFailure(result.Error);
                }
                else
                {
                    _stateMachine.TryTransitionTo(GameState.MainMenu);
                }

                Complete(onCompleted, result);
            });
        }

        public void StartNewGame(Action<SceneLoadResult> onCompleted = null)
        {
            if (State != GameState.MainMenu)
            {
                Fail(onCompleted, "A new game can only start from the main menu (current state: " + State + ").");
                return;
            }

            if (_sceneLoader.IsLoading)
            {
                Fail(onCompleted, "A scene transition is already in progress.");
                return;
            }

            _stateMachine.TransitionTo(GameState.LoadingGameplay);
            LoadScene(_configuration.Game.GameplayScene, result =>
            {
                if (!result.Succeeded)
                {
                    ReportFailure(result.Error);
                }
                else
                {
                    _stateMachine.TryTransitionTo(GameState.Gameplay);
                }

                Complete(onCompleted, result);
            });
        }

        public void ReturnToMainMenu(Action<SceneLoadResult> onCompleted = null)
        {
            if (State != GameState.Gameplay && State != GameState.Paused)
            {
                Fail(onCompleted, "Cannot return to the main menu from state " + State + ".");
                return;
            }

            if (_sceneLoader.IsLoading)
            {
                Fail(onCompleted, "A scene transition is already in progress.");
                return;
            }

            _stateMachine.TransitionTo(GameState.ReturningToMenu);
            LoadScene(_configuration.Game.MainMenuScene, result =>
            {
                if (!result.Succeeded)
                {
                    ReportFailure(result.Error);
                }
                else
                {
                    _stateMachine.TryTransitionTo(GameState.MainMenu);
                }

                Complete(onCompleted, result);
            });
        }

        public void Pause()
        {
            if (_stateMachine.TryTransitionTo(GameState.Paused))
            {
                return;
            }

            GameLog.Warning("Pause requested from state " + State + " and ignored.");
        }

        public void Resume()
        {
            if (_stateMachine.TryTransitionTo(GameState.Gameplay))
            {
                return;
            }

            GameLog.Warning("Resume requested from state " + State + " and ignored.");
        }

        /// <summary>Puts the application into a recoverable failure state.</summary>
        public void ReportFailure(string reason)
        {
            GameLog.Error("Entering failed state: " + reason);
            _eventBus.Publish(new ApplicationInitializationFailedEvent(reason));

            if (!_stateMachine.TryTransitionTo(GameState.Failed))
            {
                GameLog.Warning("Could not enter the failed state from " + State + ".");
            }
        }

        public void Shutdown()
        {
            _stateMachine.TryTransitionTo(GameState.ShuttingDown);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _stateMachine.StateChanged -= HandleStateChanged;
        }

        private void LoadScene(string sceneName, Action<SceneLoadResult> onCompleted)
        {
            if (_sceneLoader.IsLoading)
            {
                Fail(onCompleted, "A scene transition is already in progress.");
                return;
            }

            _sceneLoader.Load(sceneName, onCompleted);
        }

        private void HandleStateChanged(GameState previous, GameState current)
        {
            _eventBus.Publish(new GameStateChangedEvent(previous, current));
        }

        private void Fail(Action<SceneLoadResult> onCompleted, string reason)
        {
            GameLog.Warning(reason);
            Complete(onCompleted, SceneLoadResult.Failure(string.Empty, reason));
        }

        private static void Complete(Action<SceneLoadResult> onCompleted, SceneLoadResult result)
        {
            if (onCompleted != null)
            {
                onCompleted(result);
            }
        }
    }
}

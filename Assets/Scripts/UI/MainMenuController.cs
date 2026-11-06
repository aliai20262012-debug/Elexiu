using Nexus.Configuration;
using Nexus.Core;
using Nexus.Core.Bootstrap;
using Nexus.Core.Events;
using Nexus.Core.Logging;
using Nexus.Core.Platform;
using Nexus.Core.State;
using Nexus.SaveSystem;
using Nexus.Settings;
using UnityEngine;

namespace Nexus.UI
{
    /// <summary>
    /// Scene entry point for the main menu. Resolves its dependencies from the application
    /// context, builds the view, routes button intents to the GameManager and never performs
    /// scene loading or state transitions itself.
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        private GameManager _gameManager;
        private ISaveService _saveService;
        private ISettingsService _settingsService;
        private IPlatformInfo _platform;
        private IConfigurationService _configuration;
        private IEventBus _eventBus;
        private MainMenuView _view;

        public MainMenuView View
        {
            get { return _view; }
        }

        public string TitleText
        {
            get { return _view != null && _view.TitleLabel != null ? _view.TitleLabel.Text : string.Empty; }
        }

        public bool IsContinueInteractable
        {
            get { return _view != null && _view.ContinueButton != null && _view.ContinueButton.Interactable; }
        }

        private void Start()
        {
            if (!TryResolveServices())
            {
                return;
            }

            BuildView();
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private bool TryResolveServices()
        {
            if (!AppContext.IsReady)
            {
                GameLog.Warning(
                    "Main menu started without the bootstrap scene. Composing services locally so the scene can be " +
                    "played directly in the editor.");

                BootResult bootResult = ApplicationBootstrapper.Boot();
                if (!bootResult.Succeeded)
                {
                    ShowFailure("BOOT FAILURE", bootResult.Error);
                    return false;
                }

                AppContext.Get<GameManager>().StateMachine.TryTransitionTo(GameState.MainMenu);
            }

            bool resolved =
                AppContext.TryGet(out _gameManager) &&
                AppContext.TryGet(out _configuration) &&
                AppContext.TryGet(out _platform) &&
                AppContext.TryGet(out _settingsService) &&
                AppContext.TryGet(out _eventBus);

            if (!resolved)
            {
                ShowFailure("SERVICE RESOLUTION FAILURE", "One or more core services are missing from the application context.");
                return false;
            }

            if (!AppContext.TryGet(out _saveService))
            {
                _saveService = new NullSaveService();
            }

            return true;
        }

        private void BuildView()
        {
            UiFactory.EnsureEventSystem();

            _view = new MainMenuView(
                _configuration.Theme,
                _configuration.Game,
                _platform,
                _settingsService,
                HandleSettingsClosed);

            _view.NewGameButton.Button.onClick.AddListener(OnNewGamePressed);
            _view.ContinueButton.Button.onClick.AddListener(OnContinuePressed);
            _view.SettingsButton.Button.onClick.AddListener(OnSettingsPressed);

            if (_platform.SupportsQuit)
            {
                _view.ExitButton.Button.onClick.AddListener(OnExitPressed);
            }
            else
            {
                _view.ExitButton.RectTransform.gameObject.SetActive(false);
            }

            bool hasSave = _saveService != null && _saveService.HasAnySave;
            _view.SetContinueAvailable(hasSave, _configuration.Theme);
            _view.SetStatus(hasSave ? "Save data detected" : "No save data // continue locked");
            _view.SetInteractable(true);

            Subscribe();
        }

        private void Subscribe()
        {
            if (_eventBus == null)
            {
                return;
            }

            _eventBus.Subscribe<GameStateChangedEvent>(HandleGameStateChanged);
        }

        private void Unsubscribe()
        {
            if (_eventBus == null)
            {
                return;
            }

            _eventBus.Unsubscribe<GameStateChangedEvent>(HandleGameStateChanged);
            _eventBus = null;
        }

        private void HandleGameStateChanged(GameStateChangedEvent gameEvent)
        {
            if (this == null || _view == null)
            {
                return;
            }

            _view.SetStatus("State: " + gameEvent.CurrentState);
        }

        private void HandleSettingsClosed()
        {
            if (this == null || _view == null)
            {
                return;
            }

            _view.SetStatus("Settings applied");
        }

        private void OnNewGamePressed()
        {
            if (_view != null)
            {
                _view.SetInteractable(false);
                _view.SetStatus("Initializing simulation...");
            }

            _gameManager.StartNewGame(result =>
            {
                if (!result.Succeeded)
                {
                    ShowFailure("NEW GAME FAILURE", result.Error);
                }
            });
        }

        private void OnContinuePressed()
        {
            // Phase 01: no gameplay save payload exists yet, so Continue routes to the
            // gameplay scene when a slot is present and stays disabled otherwise.
            OnNewGamePressed();
        }

        private void OnSettingsPressed()
        {
            if (_view != null && _view.Settings != null)
            {
                _view.Settings.SetVisible(!_view.Settings.IsVisible);
            }
        }

        private void OnExitPressed()
        {
            GameLog.Info("Exit requested by the player.");
            Application.Quit();
        }

        private void ShowFailure(string title, string message)
        {
            GameLog.Error(title + ": " + message);

            if (_view != null)
            {
                _view.SetStatus(title);
                _view.SetInteractable(true);
            }

            ErrorOverlay.Show(title, message, null, _configuration != null ? _configuration.Theme : UiTheme.CreateDefault());
        }
    }
}

using System;
using NUnit.Framework;
using Nexus.Configuration;
using Nexus.Core;
using Nexus.Core.Events;
using Nexus.Core.Scenes;
using Nexus.Core.State;
using Nexus.SaveSystem;

namespace Nexus.Tests.EditMode
{
    public class GameManagerFlowTests
    {
        /// <summary>Deterministic scene loader that completes synchronously.</summary>
        private sealed class FakeSceneLoader : ISceneLoader
        {
            public string LastSceneName { get; private set; }

            public int LoadCount { get; private set; }

            public bool FailNextLoad { get; set; }

            /// <summary>Simulates a scene transition that is still running.</summary>
            public bool Busy { get; set; }

            public bool IsLoading
            {
                get { return Busy; }
            }

            public void Load(string sceneName, Action<SceneLoadResult> onCompleted)
            {
                LastSceneName = sceneName;
                LoadCount++;

                if (FailNextLoad)
                {
                    FailNextLoad = false;
                    if (onCompleted != null)
                    {
                        onCompleted(SceneLoadResult.Failure(sceneName, "Simulated scene load failure."));
                    }

                    return;
                }

                if (onCompleted != null)
                {
                    onCompleted(SceneLoadResult.Success(sceneName));
                }
            }
        }

        private FakeSceneLoader _sceneLoader;
        private GameManager _gameManager;
        private EventBus _eventBus;

        [SetUp]
        public void SetUp()
        {
            _eventBus = new EventBus();
            _sceneLoader = new FakeSceneLoader();
            _gameManager = new GameManager(
                _eventBus,
                _sceneLoader,
                TestAssets.CreateConfigurationService(),
                new NullSaveService());
        }

        [TearDown]
        public void TearDown()
        {
            _gameManager.Dispose();
            TestAssets.DestroyAll();
        }

        [Test]
        public void Initialize_MovesToBooting()
        {
            _gameManager.Initialize();

            Assert.AreEqual(GameState.Booting, _gameManager.State);
        }

        [Test]
        public void Initialize_Twice_KeepsBootingState()
        {
            _gameManager.Initialize();
            _gameManager.Initialize();

            Assert.AreEqual(GameState.Booting, _gameManager.State);
        }

        [Test]
        public void EnterMainMenu_LoadsMenuScene_AndReachesMainMenu()
        {
            _gameManager.Initialize();
            _gameManager.EnterMainMenu();

            Assert.AreEqual(SceneNames.MainMenu, _sceneLoader.LastSceneName);
            Assert.AreEqual(GameState.MainMenu, _gameManager.State);
        }

        [Test]
        public void StartNewGame_LoadsGameplayScene_AndReachesGameplay()
        {
            _gameManager.Initialize();
            _gameManager.EnterMainMenu();
            _gameManager.StartNewGame();

            Assert.AreEqual(SceneNames.Gameplay, _sceneLoader.LastSceneName);
            Assert.AreEqual(GameState.Gameplay, _gameManager.State);
        }

        [Test]
        public void StartNewGame_FromBooting_IsRejectedWithoutLoading()
        {
            _gameManager.Initialize();

            SceneLoadResult result = default(SceneLoadResult);
            bool completed = false;
            _gameManager.StartNewGame(loadResult =>
            {
                result = loadResult;
                completed = true;
            });

            Assert.IsTrue(completed);
            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(0, _sceneLoader.LoadCount);
            Assert.AreEqual(GameState.Booting, _gameManager.State);
        }

        [Test]
        public void ReturnToMainMenu_FromGameplay_LoadsMenuScene()
        {
            _gameManager.Initialize();
            _gameManager.EnterMainMenu();
            _gameManager.StartNewGame();
            _gameManager.ReturnToMainMenu();

            Assert.AreEqual(SceneNames.MainMenu, _sceneLoader.LastSceneName);
            Assert.AreEqual(GameState.MainMenu, _gameManager.State);
        }

        [Test]
        public void SceneLoadFailure_MovesToFailedState()
        {
            _gameManager.Initialize();
            _sceneLoader.FailNextLoad = true;

            _gameManager.EnterMainMenu();

            Assert.AreEqual(GameState.Failed, _gameManager.State);
        }

        [Test]
        public void PauseAndResume_OnlyWorkDuringGameplay()
        {
            _gameManager.Initialize();
            _gameManager.EnterMainMenu();
            _gameManager.StartNewGame();

            _gameManager.Pause();
            Assert.AreEqual(GameState.Paused, _gameManager.State);

            _gameManager.Resume();
            Assert.AreEqual(GameState.Gameplay, _gameManager.State);

            _gameManager.ReturnToMainMenu();
            _gameManager.Pause();
            Assert.AreEqual(GameState.MainMenu, _gameManager.State, "Pause must be ignored outside gameplay.");
        }

        [Test]
        public void StateChangedEvent_IsPublishedOnTheEventBus()
        {
            bool observed = false;
            GameState observedState = GameState.Uninitialized;

            _eventBus.Subscribe<GameStateChangedEvent>(gameEvent =>
            {
                observed = true;
                observedState = gameEvent.CurrentState;
            });

            _gameManager.Initialize();

            Assert.IsTrue(observed);
            Assert.AreEqual(GameState.Booting, observedState);
        }

        [Test]
        public void StartNewGame_WhileSceneLoadInProgress_IsRejectedWithoutStateChange()
        {
            _gameManager.Initialize();
            _gameManager.EnterMainMenu();

            _sceneLoader.Busy = true;
            int loadsBefore = _sceneLoader.LoadCount;

            SceneLoadResult result = default(SceneLoadResult);
            _gameManager.StartNewGame(loadResult => result = loadResult);

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(loadsBefore, _sceneLoader.LoadCount);
            Assert.AreEqual(GameState.MainMenu, _gameManager.State);
        }

        [Test]
        public void CanContinue_IsFalseWithoutSaves()
        {
            Assert.IsFalse(_gameManager.CanContinue);
        }
    }
}

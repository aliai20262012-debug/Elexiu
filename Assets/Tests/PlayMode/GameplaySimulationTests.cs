using System.Collections;
using NUnit.Framework;
using Nexus.Core;
using Nexus.Core.Events;
using Nexus.Core.Scenes;
using Nexus.Core.State;
using Nexus.Simulation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Nexus.Tests.PlayMode
{
    /// <summary>
    /// End-to-end checks for the Phase 02 core: the gameplay scene must create a session,
    /// run the deterministic clock, honour pause/speed controls through the HUD and tear the
    /// session down safely when the player returns to the command interface.
    /// </summary>
    public class GameplaySimulationTests
    {
        private static GameplaySceneController _controller;

        [SetUp]
        public void SetUp()
        {
            _controller = null;

            if (AppContext.IsReady)
            {
                AppContext.Uninstall();
            }
        }

        [TearDown]
        public void TearDown()
        {
            if (AppContext.IsReady)
            {
                AppContext.Uninstall();
            }
        }

        private static IEnumerator StartGameplay()
        {
            yield return SceneTestUtility.BootToMainMenu();

            GameManager manager;
            Assert.IsTrue(AppContext.TryGet(out manager), "The bootstrap flow must compose the GameManager.");
            Assert.AreEqual(GameState.MainMenu, manager.State);

            manager.StartNewGame();

            float timeout = SceneTestUtility.DefaultTimeoutSeconds;
            while (timeout > 0f && SceneManager.GetActiveScene().name != SceneNames.Gameplay)
            {
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.AreEqual(SceneNames.Gameplay, SceneManager.GetActiveScene().name, "New Game must open the gameplay scene.");

            timeout = SceneTestUtility.DefaultTimeoutSeconds;
            while (timeout > 0f)
            {
                _controller = Object.FindFirstObjectByType<GameplaySceneController>();
                if (_controller != null && _controller.Session != null)
                {
                    break;
                }

                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.IsNotNull(_controller, "The gameplay scene must contain a GameplaySceneController.");
            Assert.IsNotNull(_controller.Session, "Entering gameplay must create a game session.");
        }

        [UnityTest]
        public IEnumerator Gameplay_StartsSessionAndSimulationClock()
        {
            yield return StartGameplay();

            ISimulationManager simulation;
            Assert.IsTrue(AppContext.TryGet(out simulation));

            Assert.IsTrue(_controller.Session.IsActive);
            Assert.IsTrue(_controller.IsSimulationRunning);
            Assert.AreEqual(SimulationStatus.Running, simulation.Status);
            Assert.AreEqual(_controller.Session.SessionId, simulation.SessionId);

            Assert.IsNotNull(_controller.Hud, "The gameplay scene must build its HUD.");
            Assert.IsNotNull(_controller.Hud.ControlBar, "The HUD must expose the simulation controls.");
            Assert.IsNotNull(_controller.Hud.ReturnToMenuButton);
            Assert.GreaterOrEqual(_controller.Hud.ControlBar.SpeedButtons.Count, 2, "Configured speed levels must be rendered.");
            StringAssert.StartsWith("T+ ", _controller.Hud.ControlBar.ClockText);
            StringAssert.Contains("RUNNING", _controller.Hud.ControlBar.StatusText);

            long before = simulation.TickCount;
            float timeout = 5f;
            while (timeout > 0f && simulation.TickCount <= before)
            {
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.Greater(simulation.TickCount, before, "The simulation clock must advance while gameplay is running.");
            Assert.AreEqual(simulation.TickCount, _controller.Session.TickCount, "Session progress must follow the simulation.");
        }

        [UnityTest]
        public IEnumerator Gameplay_PauseResumeAndSpeedControls()
        {
            yield return StartGameplay();

            ISimulationManager simulation;
            Assert.IsTrue(AppContext.TryGet(out simulation));

            GameManager manager;
            Assert.IsTrue(AppContext.TryGet(out manager));

            Assert.IsTrue(_controller.ToggleSimulationPause());
            Assert.AreEqual(SimulationStatus.Paused, simulation.Status);
            Assert.AreEqual(GameSessionState.Paused, _controller.Session.State);
            Assert.AreEqual(GameState.Paused, manager.State);
            StringAssert.Contains("PAUSED", _controller.Hud.ControlBar.StatusText);

            long pausedTick = simulation.TickCount;
            yield return null;
            yield return null;
            yield return null;
            Assert.AreEqual(pausedTick, simulation.TickCount, "A paused simulation must not advance.");

            Assert.IsTrue(_controller.SelectSpeed(2.0));
            Assert.AreEqual(2.0, simulation.SpeedMultiplier, 0.0001);
            StringAssert.Contains("2x", _controller.Hud.ControlBar.StatusText);

            Assert.IsTrue(_controller.ToggleSimulationPause());
            Assert.AreEqual(SimulationStatus.Running, simulation.Status);
            Assert.AreEqual(GameSessionState.Active, _controller.Session.State);
            Assert.AreEqual(GameState.Gameplay, manager.State);
        }

        [UnityTest]
        public IEnumerator Gameplay_ReturnToMenu_EndsSessionSafely()
        {
            yield return StartGameplay();

            ISimulationManager simulation;
            Assert.IsTrue(AppContext.TryGet(out simulation));

            IGameSessionService sessions;
            Assert.IsTrue(AppContext.TryGet(out sessions));

            IEventBus eventBus;
            Assert.IsTrue(AppContext.TryGet(out eventBus));

            GameManager manager;
            Assert.IsTrue(AppContext.TryGet(out manager));

            string sessionId = sessions.Current.SessionId;
            string endedSessionId = null;
            eventBus.Subscribe<GameSessionEndedEvent>(gameEvent => endedSessionId = gameEvent.SessionId);

            manager.ReturnToMainMenu();

            float timeout = SceneTestUtility.DefaultTimeoutSeconds;
            while (timeout > 0f && SceneManager.GetActiveScene().name != SceneNames.MainMenu)
            {
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.AreEqual(SceneNames.MainMenu, SceneManager.GetActiveScene().name, "Returning must reopen the main menu.");
            Assert.AreEqual(sessionId, endedSessionId, "Leaving gameplay must publish the session end event.");
            Assert.IsNull(sessions.Current, "No session may survive a scene transition.");
            Assert.IsFalse(sessions.HasActiveSession);
            Assert.AreEqual(SimulationStatus.Stopped, simulation.Status);
            Assert.AreEqual(0L, simulation.TickCount);

            // Clean restart: a second session must start from scratch instead of reusing state.
            manager.StartNewGame();

            timeout = SceneTestUtility.DefaultTimeoutSeconds;
            GameplaySceneController secondController = null;
            while (timeout > 0f)
            {
                secondController = Object.FindFirstObjectByType<GameplaySceneController>();
                if (secondController != null && secondController.Session != null)
                {
                    break;
                }

                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.IsNotNull(secondController);
            Assert.IsNotNull(secondController.Session);
            Assert.AreNotEqual(sessionId, secondController.Session.SessionId);
            Assert.AreEqual(SimulationStatus.Running, simulation.Status);
        }
    }
}

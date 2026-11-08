using System.Collections;
using NUnit.Framework;
using Nexus.Core;
using Nexus.Core.Events;
using Nexus.Core.Platform;
using Nexus.Core.Scenes;
using Nexus.Core.State;
using Nexus.SaveSystem;
using Nexus.Settings;
using Nexus.Simulation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Nexus.Tests.PlayMode
{
    /// <summary>
    /// End to end verification of the Phase 01 shell: the bootstrap scene must install every
    /// service and hand control to the main menu without blocking errors.
    /// </summary>
    public class BootstrapFlowTests
    {
        [SetUp]
        public void SetUp()
        {
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

        [UnityTest]
        public IEnumerator BootstrapScene_InitializesServices_AndOpensMainMenu()
        {
            yield return SceneTestUtility.BootToMainMenu();

            Assert.IsTrue(AppContext.IsReady, "The bootstrap scene did not install the application services.");

            Assert.IsTrue(AppContext.TryGet(out IEventBus eventBus));
            Assert.IsTrue(AppContext.TryGet(out GameManager gameManager));
            Assert.IsTrue(AppContext.TryGet(out ISceneLoader sceneLoader));
            Assert.IsTrue(AppContext.TryGet(out ISimulationManager simulationManager));
            Assert.IsTrue(AppContext.TryGet(out ISaveService saveService));
            Assert.IsTrue(AppContext.TryGet(out ISettingsService settingsService));
            Assert.IsTrue(AppContext.TryGet(out IPlatformInfo platformInfo));

            Assert.IsNotNull(eventBus);
            Assert.IsNotNull(sceneLoader);
            Assert.IsNotNull(simulationManager);
            Assert.IsNotNull(saveService);
            Assert.IsNotNull(settingsService);
            Assert.IsNotNull(platformInfo);
            Assert.AreEqual(GameState.MainMenu, gameManager.State);
            Assert.AreEqual(SceneNames.MainMenu, SceneManager.GetActiveScene().name);
        }

        [UnityTest]
        public IEnumerator ApplicationServices_SurviveSceneChanges()
        {
            yield return SceneTestUtility.BootToMainMenu();

            GameManager firstManager;
            Assert.IsTrue(AppContext.TryGet(out firstManager));

            firstManager.StartNewGame();

            float timeout = SceneTestUtility.DefaultTimeoutSeconds;
            while (timeout > 0f && SceneManager.GetActiveScene().name != SceneNames.Gameplay)
            {
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.AreEqual(SceneNames.Gameplay, SceneManager.GetActiveScene().name);
            Assert.IsTrue(AppContext.IsReady, "Services were lost when the gameplay scene loaded.");

            firstManager.ReturnToMainMenu();

            timeout = SceneTestUtility.DefaultTimeoutSeconds;
            while (timeout > 0f && SceneManager.GetActiveScene().name != SceneNames.MainMenu)
            {
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }

            GameManager secondManager;
            Assert.IsTrue(AppContext.TryGet(out secondManager));
            Assert.AreSame(firstManager, secondManager, "The GameManager must survive scene transitions.");
        }
    }
}

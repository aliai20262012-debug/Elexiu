using System.Collections;
using NUnit.Framework;
using Nexus.Core;
using Nexus.Core.Scenes;
using Nexus.Core.State;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Nexus.Tests.PlayMode
{
    public class SceneNavigationTests
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
        public IEnumerator NewGame_NavigatesToGameplay_AndBack()
        {
            yield return SceneTestUtility.BootToMainMenu();

            GameManager gameManager;
            Assert.IsTrue(AppContext.TryGet(out gameManager));
            Assert.AreEqual(GameState.MainMenu, gameManager.State);

            gameManager.StartNewGame();

            float timeout = SceneTestUtility.DefaultTimeoutSeconds;
            while (timeout > 0f && SceneManager.GetActiveScene().name != SceneNames.Gameplay)
            {
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.AreEqual(SceneNames.Gameplay, SceneManager.GetActiveScene().name, "New Game must open the gameplay scene.");

            yield return null;
            Assert.AreEqual(GameState.Gameplay, gameManager.State);

            gameManager.ReturnToMainMenu();

            timeout = SceneTestUtility.DefaultTimeoutSeconds;
            while (timeout > 0f && SceneManager.GetActiveScene().name != SceneNames.MainMenu)
            {
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.AreEqual(SceneNames.MainMenu, SceneManager.GetActiveScene().name, "Return to menu must reopen the main menu scene.");
            Assert.AreEqual(GameState.MainMenu, gameManager.State);
        }
    }
}

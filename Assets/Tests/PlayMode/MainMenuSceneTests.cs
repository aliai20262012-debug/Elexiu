using System.Collections;
using NUnit.Framework;
using Nexus.Core;
using Nexus.SaveSystem;
using Nexus.UI;
using UnityEngine;
using UnityEngine.TestTools;

namespace Nexus.Tests.PlayMode
{
    public class MainMenuSceneTests
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
        public IEnumerator MainMenu_ShowsTitleAndButtons()
        {
            yield return SceneTestUtility.BootToMainMenu();

            MainMenuController controller = Object.FindFirstObjectByType<MainMenuController>();

            Assert.IsNotNull(controller, "The main menu scene must contain a MainMenuController.");
            StringAssert.Contains("NEXUS", controller.TitleText);
            Assert.IsNotNull(controller.View);
            Assert.IsNotNull(controller.View.NewGameButton);
            Assert.IsNotNull(controller.View.SettingsButton);
            Assert.IsNotNull(Object.FindFirstObjectByType<Canvas>(), "The main menu must create a canvas.");
        }

        [UnityTest]
        public IEnumerator MainMenu_ContinueIsDisabled_WithoutSaveData()
        {
            yield return SceneTestUtility.BootToMainMenu();

            MainMenuController controller = Object.FindFirstObjectByType<MainMenuController>();

            Assert.IsNotNull(controller);

            ISaveService saveService;
            bool hasSave = AppContext.TryGet(out saveService) && saveService.HasAnySave;

            // Invariant: Continue reflects save availability. In Phase 01 nothing writes a save,
            // so this also proves the button stays disabled on a clean machine.
            Assert.AreEqual(hasSave, controller.IsContinueInteractable, "Continue must reflect save availability.");
        }
    }
}

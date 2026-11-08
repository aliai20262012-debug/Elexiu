using System.Collections;
using NUnit.Framework;
using Nexus.Core;
using Nexus.Core.Scenes;
using Nexus.Core.State;
using Nexus.Planet;
using Nexus.Simulation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Nexus.Tests.PlayMode
{
    /// <summary>
    /// End-to-end checks for the Phase 03 planet: the gameplay scene must build the procedural
    /// planet, wire the orbit camera and input, show the region panel on selection and return
    /// cleanly to the overview.
    /// </summary>
    public class PlanetPlayModeTests
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

        private static IEnumerator StartGameplayWithPlanet()
        {
            yield return SceneTestUtility.BootToMainMenu();

            GameManager manager;
            Assert.IsTrue(AppContext.TryGet(out manager), "Bootstrap must compose the GameManager.");
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
                if (_controller != null &&
                    _controller.Session != null &&
                    _controller.Planet != null &&
                    _controller.Planet.IsBuilt &&
                    _controller.PlanetScene != null &&
                    _controller.PlanetScene.IsInitialized)
                {
                    break;
                }

                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.IsNotNull(_controller, "The gameplay scene must contain a GameplaySceneController.");
            Assert.IsNotNull(_controller.Planet, "The gameplay scene must own a planet.");
            Assert.IsTrue(_controller.Planet.IsBuilt, "The planet must be built once the session starts.");
            Assert.IsNotNull(_controller.PlanetScene, "The planet scene controller must be created.");
        }

        [UnityTest]
        public IEnumerator Gameplay_BuildsPlanetWithRegionsAndCamera()
        {
            yield return StartGameplayWithPlanet();

            PlanetSceneController scene = _controller.PlanetScene;
            PlanetView view = _controller.Planet.View;

            Assert.IsNotNull(view);
            Assert.GreaterOrEqual(view.Regions.Count, 3, "The planet must expose multiple regions.");
            Assert.IsNotNull(scene.CameraController);
            Assert.IsTrue(scene.CameraController.IsConfigured);
            Assert.Greater(scene.CameraController.Distance, view.Radius, "The camera must sit outside the planet.");
            Assert.IsNotNull(scene.Panel);
            Assert.IsFalse(scene.Panel.IsVisible, "The region panel starts hidden.");

            yield return null;
        }

        [UnityTest]
        public IEnumerator RegionSelection_ShowsPanelAndFocusesCamera()
        {
            yield return StartGameplayWithPlanet();

            PlanetSceneController scene = _controller.PlanetScene;
            PlanetRegion region = _controller.Planet.Regions[0];

            scene.HandleRegionActivated(region);
            yield return null;

            Assert.AreEqual(region.Id, scene.SelectedRegionId);
            Assert.IsTrue(scene.Panel.IsVisible, "Selecting a region must open the region panel.");
            Assert.AreEqual(region.Name, scene.Panel.NameText);
            StringAssert.Contains(region.Code, scene.Panel.HeaderText);
            StringAssert.Contains("HABITABILITY", scene.Panel.BodyText);
            Assert.AreEqual(PlanetViewMode.RegionFocus, scene.Mode);
            Assert.Less(
                scene.CameraController.TargetDistance,
                _controller.Planet.View.Radius * 3f,
                "Region focus must move the camera closer than the overview.");

            yield return null;
        }

        [UnityTest]
        public IEnumerator ReturnToOverview_ClearsSelectionAndPanel()
        {
            yield return StartGameplayWithPlanet();

            PlanetSceneController scene = _controller.PlanetScene;
            scene.HandleRegionActivated(_controller.Planet.Regions[1]);
            yield return null;

            scene.ReturnToOverview();
            yield return null;

            Assert.IsFalse(scene.Panel.IsVisible);
            Assert.AreEqual(RegionLocator.NoRegion, scene.SelectedRegionId);
            Assert.AreEqual(PlanetViewMode.Overview, scene.Mode);
            Assert.IsFalse(_controller.Planet.View.HasSelectionHighlight);
        }

        [UnityTest]
        public IEnumerator ScreenCentreTap_SelectsARegionThroughInput()
        {
            yield return StartGameplayWithPlanet();

            PlanetSceneController scene = _controller.PlanetScene;
            yield return null;

            bool selected = scene.Input.SelectAtScreenPoint(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
            yield return null;

            Assert.IsTrue(selected, "Tapping the planet must resolve to a region.");
            Assert.Greater(scene.SelectedRegionId, 0);
            Assert.IsTrue(scene.Panel.IsVisible);
            Assert.AreEqual(PlanetViewMode.RegionFocus, scene.Mode);
        }
    }
}

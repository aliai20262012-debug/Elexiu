using System.Collections.Generic;
using NUnit.Framework;
using Nexus.Core.Scenes;
using UnityEditor;

namespace Nexus.Tests.EditMode
{
    /// <summary>
    /// Guards the scene navigation contract: the bootstrap scene must be first and every
    /// navigable scene must be included in the build settings, otherwise the runtime
    /// SceneLoader will refuse the load.
    /// </summary>
    public class BuildSettingsTests
    {
        [Test]
        public void BuildSettings_ContainAllNavigableScenes()
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            List<string> paths = new List<string>();
            for (int i = 0; i < scenes.Length; i++)
            {
                paths.Add(scenes[i].path);
            }

            for (int i = 0; i < SceneNames.All.Length; i++)
            {
                string expectedSuffix = "/" + SceneNames.All[i] + ".unity";
                bool found = false;
                for (int p = 0; p < paths.Count; p++)
                {
                    if (paths[p] != null && paths[p].EndsWith(expectedSuffix))
                    {
                        found = true;
                        break;
                    }
                }

                Assert.IsTrue(found, "Scene '" + SceneNames.All[i] + "' is missing from the build settings.");
            }
        }

        [Test]
        public void BuildSettings_BootstrapSceneIsFirst()
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;

            Assert.IsNotEmpty(scenes, "The build settings must contain at least one scene.");
            Assert.IsTrue(scenes[0].enabled, "The bootstrap scene must be enabled.");
            StringAssert.Contains(SceneNames.Bootstrap, scenes[0].path);
        }

        [Test]
        public void SceneAssets_ExistOnDisk()
        {
            for (int i = 0; i < SceneNames.All.Length; i++)
            {
                string[] guids = AssetDatabase.FindAssets(SceneNames.All[i] + " t:Scene");
                Assert.IsTrue(guids.Length > 0, "No scene asset named '" + SceneNames.All[i] + "' was found.");
            }
        }

        [Test]
        public void RuntimeAssemblies_AreDiscovered()
        {
            Assert.IsNotNull(typeof(Nexus.Core.GameManager).Assembly);
            Assert.IsNotNull(typeof(Nexus.UI.UiFactory).Assembly);
            Assert.IsNotNull(typeof(Nexus.Simulation.SimulationManager).Assembly);
            Assert.IsNotNull(typeof(Nexus.Planet.PlanetViewController).Assembly);
        }
    }
}

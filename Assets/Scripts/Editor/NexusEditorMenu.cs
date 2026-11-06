using System.IO;
using Nexus.Configuration;
using Nexus.Core.Scenes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Nexus.EditorTools
{
    /// <summary>Small quality-of-life menu for the team; no runtime behaviour.</summary>
    public static class NexusEditorMenu
    {
        private const string ConfigFolder = "Assets/Resources/Config";

        [MenuItem("Nexus/Open Bootstrap Scene", false, 0)]
        public static void OpenBootstrapScene()
        {
            string path = "Assets/Scenes/Bootstrap/" + SceneNames.Bootstrap + ".unity";
            if (!File.Exists(path))
            {
                Debug.LogError("[NEXUS] Bootstrap scene not found at " + path + ".");
                return;
            }

            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(path);
            }
        }

        [MenuItem("Nexus/Open Main Menu Scene", false, 1)]
        public static void OpenMainMenuScene()
        {
            string path = "Assets/Scenes/MainMenu/" + SceneNames.MainMenu + ".unity";
            if (!File.Exists(path))
            {
                Debug.LogError("[NEXUS] Main menu scene not found at " + path + ".");
                return;
            }

            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(path);
            }
        }

        [MenuItem("Nexus/Open Gameplay Scene", false, 2)]
        public static void OpenGameplayScene()
        {
            string path = "Assets/Scenes/Gameplay/" + SceneNames.Gameplay + ".unity";
            if (!File.Exists(path))
            {
                Debug.LogError("[NEXUS] Gameplay scene not found at " + path + ".");
                return;
            }

            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(path);
            }
        }

        [MenuItem("Nexus/Create Default Configuration Assets", false, 40)]
        public static void CreateDefaultConfigurationAssets()
        {
            Directory.CreateDirectory(ConfigFolder);

            CreateAssetIfMissing<GameConfiguration>(ConfigFolder + "/GameConfiguration.asset");
            CreateAssetIfMissing<UiTheme>(ConfigFolder + "/UiTheme.asset");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[NEXUS] Default configuration assets are in place under " + ConfigFolder + ".");
        }

        private static void CreateAssetIfMissing<T>(string path) where T : ScriptableObject
        {
            if (AssetDatabase.LoadAssetAtPath<T>(path) != null)
            {
                return;
            }

            T asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
        }
    }
}

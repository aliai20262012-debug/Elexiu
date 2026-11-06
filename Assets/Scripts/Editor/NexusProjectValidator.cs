using System.Collections.Generic;
using System.Text;
using Nexus.Configuration;
using Nexus.Core.Scenes;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nexus.EditorTools
{
    /// <summary>
    /// Project health checks. Runs from the "Nexus" menu and from CI via
    /// <c>-executeMethod Nexus.EditorTools.NexusProjectValidator.ValidateBatchMode</c>.
    /// </summary>
    public static class NexusProjectValidator
    {
        public sealed class Issue
        {
            public readonly bool IsError;
            public readonly string Message;

            public Issue(bool isError, string message)
            {
                IsError = isError;
                Message = message;
            }

            public override string ToString()
            {
                return (IsError ? "[ERROR] " : "[WARN ] ") + Message;
            }
        }

        [MenuItem("Nexus/Validate Project", false, 20)]
        public static void ValidateFromMenu()
        {
            List<Issue> issues = Validate();
            StringBuilder builder = new StringBuilder();
            int errors = 0;

            for (int i = 0; i < issues.Count; i++)
            {
                builder.AppendLine(issues[i].ToString());
                if (issues[i].IsError)
                {
                    errors++;
                }
            }

            if (errors == 0)
            {
                Debug.Log("[NEXUS] Validation passed with " + (issues.Count - errors) + " warning(s).\n" + builder);
            }
            else
            {
                Debug.LogError("[NEXUS] Validation failed with " + errors + " error(s).\n" + builder);
            }
        }

        /// <summary>CI entry point: exits with code 0 on success, 1 on any error.</summary>
        public static void ValidateBatchMode()
        {
            List<Issue> issues = Validate();
            int errors = 0;

            for (int i = 0; i < issues.Count; i++)
            {
                if (issues[i].IsError)
                {
                    errors++;
                    Debug.LogError("[NEXUS][VALIDATION] " + issues[i].Message);
                }
                else
                {
                    Debug.LogWarning("[NEXUS][VALIDATION] " + issues[i].Message);
                }
            }

            Debug.Log("[NEXUS][VALIDATION] Completed with " + errors + " error(s) and " + (issues.Count - errors) + " warning(s).");
            EditorApplication.Exit(errors == 0 ? 0 : 1);
        }

        public static List<Issue> Validate()
        {
            List<Issue> issues = new List<Issue>();
            ValidateScenes(issues);
            ValidateRenderPipeline(issues);
            ValidateConfigurationAssets(issues);
            ValidateTextMeshPro(issues);
            return issues;
        }

        private static void ValidateScenes(List<Issue> issues)
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            if (scenes == null || scenes.Length == 0)
            {
                issues.Add(new Issue(true, "No scenes are listed in the build settings."));
                return;
            }

            for (int i = 0; i < SceneNames.All.Length; i++)
            {
                string expectedName = SceneNames.All[i];
                bool found = false;

                for (int j = 0; j < scenes.Length; j++)
                {
                    if (scenes[j].path != null && scenes[j].path.EndsWith("/" + expectedName + ".unity"))
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    issues.Add(new Issue(true, "Scene '" + expectedName + "' is missing from the build settings."));
                }
            }

            if (scenes[0].path == null || !scenes[0].path.EndsWith("/" + SceneNames.Bootstrap + ".unity"))
            {
                issues.Add(new Issue(true, "The Bootstrap scene must be the first entry in the build settings."));
            }
        }

        private static void ValidateRenderPipeline(List<Issue> issues)
        {
            if (GraphicsSettings.defaultRenderPipeline == null)
            {
                issues.Add(new Issue(true, "No Scriptable Render Pipeline is assigned in Graphics settings. Assign Assets/Settings/PC_RPAsset.asset."));
            }

            if (QualitySettings.names == null || QualitySettings.names.Length == 0)
            {
                issues.Add(new Issue(true, "No quality levels are defined."));
            }
        }

        private static void ValidateConfigurationAssets(List<Issue> issues)
        {
            GameConfiguration game = Resources.Load<GameConfiguration>(ConfigurationService.GameConfigurationResourcePath);
            if (game == null)
            {
                issues.Add(new Issue(false, "Resources/" + ConfigurationService.GameConfigurationResourcePath + " is missing; runtime defaults will be used."));
            }

            UiTheme theme = Resources.Load<UiTheme>(ConfigurationService.UiThemeResourcePath);
            if (theme == null)
            {
                issues.Add(new Issue(false, "Resources/" + ConfigurationService.UiThemeResourcePath + " is missing; the built-in palette will be used."));
            }
        }

        private static void ValidateTextMeshPro(List<Issue> issues)
        {
            string[] settings = AssetDatabase.FindAssets("t:TMP_Settings");
            if (settings == null || settings.Length == 0)
            {
                issues.Add(new Issue(false, "TextMeshPro Essential Resources are not imported (Window > TextMeshPro > Import TMP Essential Resources). The shell falls back to legacy text until then."));
            }
        }
    }
}

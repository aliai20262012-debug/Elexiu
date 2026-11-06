using System.Collections;
using Nexus.Core;
using Nexus.Core.Scenes;
using Nexus.Core.State;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nexus.Tests.PlayMode
{
    /// <summary>Shared helpers for the play mode scene tests.</summary>
    internal static class SceneTestUtility
    {
        public const float DefaultTimeoutSeconds = 30f;

        /// <summary>Loads the bootstrap scene and waits until the application reaches the main menu.</summary>
        public static IEnumerator BootToMainMenu()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync(SceneNames.Bootstrap, LoadSceneMode.Single);
            while (load != null && !load.isDone)
            {
                yield return null;
            }

            float timeout = DefaultTimeoutSeconds;
            while (timeout > 0f)
            {
                if (AppContext.IsReady)
                {
                    GameManager manager;
                    if (AppContext.TryGet(out manager) && manager.State == GameState.MainMenu)
                    {
                        break;
                    }
                }

                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }
        }

        public static bool IsMainMenuActive()
        {
            return SceneManager.GetActiveScene().name == SceneNames.MainMenu;
        }
    }
}

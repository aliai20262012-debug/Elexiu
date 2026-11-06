using System;
using System.Collections;
using Nexus.Core.Events;
using Nexus.Core.Logging;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nexus.Core.Scenes
{
    /// <summary>
    /// Unity implementation of <see cref="ISceneLoader"/>.
    /// Lives on a DontDestroyOnLoad object created by the bootstrap so that it survives
    /// the scene it was created in.
    /// </summary>
    public sealed class SceneLoader : MonoBehaviour, ISceneLoader
    {
        private IEventBus _eventBus;
        private bool _isLoading;

        public bool IsLoading
        {
            get { return _isLoading; }
        }

        /// <summary>Creates the persistent loader object and returns its component.</summary>
        public static SceneLoader Create(IEventBus eventBus)
        {
            GameObject host = new GameObject("[SceneLoader]");
            DontDestroyOnLoad(host);
            SceneLoader loader = host.AddComponent<SceneLoader>();
            loader._eventBus = eventBus;
            return loader;
        }

        public void Load(string sceneName, Action<SceneLoadResult> onCompleted)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                Notify(onCompleted, SceneLoadResult.Failure(sceneName, "Scene name is empty."));
                return;
            }

            if (_isLoading)
            {
                Notify(onCompleted, SceneLoadResult.Failure(sceneName, "Another scene load is already in progress."));
                return;
            }

            StartCoroutine(LoadRoutine(sceneName, onCompleted));
        }

        private IEnumerator LoadRoutine(string sceneName, Action<SceneLoadResult> onCompleted)
        {
            _isLoading = true;
            Publish(new SceneLoadRequestedEvent(sceneName));

            AsyncOperation operation = null;
            try
            {
                operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            }
            catch (Exception exception)
            {
                GameLog.Exception(exception, "SceneManager refused the request for scene '" + sceneName + "'.");
            }

            if (operation == null)
            {
                _isLoading = false;
                SceneLoadResult failure = SceneLoadResult.Failure(
                    sceneName,
                    "Scene '" + sceneName + "' could not be loaded. Verify it exists and is included in the build settings.");

                Publish(new SceneLoadCompletedEvent(sceneName, false, failure.Error));
                Notify(onCompleted, failure);
                yield break;
            }

            while (!operation.isDone)
            {
                yield return null;
            }

            _isLoading = false;
            Publish(new SceneLoadCompletedEvent(sceneName, true, string.Empty));
            Notify(onCompleted, SceneLoadResult.Success(sceneName));
        }

        private void Publish<TEvent>(TEvent gameEvent) where TEvent : struct, IGameEvent
        {
            if (_eventBus != null)
            {
                _eventBus.Publish(gameEvent);
            }
        }

        private static void Notify(Action<SceneLoadResult> onCompleted, SceneLoadResult result)
        {
            if (onCompleted != null)
            {
                onCompleted(result);
            }
        }
    }
}

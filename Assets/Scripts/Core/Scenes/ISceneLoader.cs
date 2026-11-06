using System;

namespace Nexus.Core.Scenes
{
    /// <summary>
    /// Abstraction over scene navigation so that game flow can be tested
    /// with a fake loader and so that presentation never calls the SceneManager directly.
    /// </summary>
    public interface ISceneLoader
    {
        bool IsLoading { get; }

        void Load(string sceneName, Action<SceneLoadResult> onCompleted);
    }
}

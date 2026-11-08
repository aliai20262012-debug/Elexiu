namespace Nexus.Core.Scenes
{
    /// <summary>Outcome of a scene load request.</summary>
    public readonly struct SceneLoadResult
    {
        public readonly string SceneName;
        public readonly bool Succeeded;
        public readonly string Error;

        private SceneLoadResult(string sceneName, bool succeeded, string error)
        {
            SceneName = sceneName;
            Succeeded = succeeded;
            Error = error;
        }

        public static SceneLoadResult Success(string sceneName)
        {
            return new SceneLoadResult(sceneName, true, string.Empty);
        }

        public static SceneLoadResult Failure(string sceneName, string error)
        {
            return new SceneLoadResult(sceneName, false, string.IsNullOrEmpty(error) ? "Unknown scene load failure." : error);
        }

        public override string ToString()
        {
            return Succeeded ? "Loaded '" + SceneName + "'." : "Failed to load '" + SceneName + "': " + Error;
        }
    }
}

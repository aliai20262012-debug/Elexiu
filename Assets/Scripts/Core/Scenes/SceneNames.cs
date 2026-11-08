namespace Nexus.Core.Scenes
{
    /// <summary>
    /// Scene identifiers. Scenes are always requested by name through
    /// <see cref="ISceneLoader"/> so that no gameplay code references UnityEngine.SceneManagement directly.
    /// </summary>
    public static class SceneNames
    {
        public const string Bootstrap = "Bootstrap";
        public const string MainMenu = "MainMenu";
        public const string Gameplay = "Gameplay";

        public static readonly string[] All = { Bootstrap, MainMenu, Gameplay };
    }
}

using Nexus.Core.Logging;
using UnityEngine;

namespace Nexus.Configuration
{
    /// <summary>
    /// Loads configuration assets from Resources and degrades gracefully to
    /// validated, in-memory defaults when an asset is missing.
    /// </summary>
    public sealed class ConfigurationService : IConfigurationService
    {
        public const string GameConfigurationResourcePath = "Config/GameConfiguration";
        public const string UiThemeResourcePath = "Config/UiTheme";

        public GameConfiguration Game { get; private set; }

        public UiTheme Theme { get; private set; }

        public ConfigurationService(GameConfiguration game, UiTheme theme)
        {
            Game = game != null ? game : GameConfiguration.CreateDefault();
            Theme = theme != null ? theme : UiTheme.CreateDefault();
        }

        public static ConfigurationService LoadOrDefaults()
        {
            GameConfiguration game = Resources.Load<GameConfiguration>(GameConfigurationResourcePath);
            if (game == null)
            {
                GameLog.Warning(
                    "No GameConfiguration found at Resources/" + GameConfigurationResourcePath +
                    ". Using validated runtime defaults.");
                game = GameConfiguration.CreateDefault();
            }

            UiTheme theme = Resources.Load<UiTheme>(UiThemeResourcePath);
            if (theme == null)
            {
                GameLog.Warning(
                    "No UiTheme found at Resources/" + UiThemeResourcePath + ". Using the built-in dark sci-fi palette.");
                theme = UiTheme.CreateDefault();
            }

            return new ConfigurationService(game, theme);
        }
    }
}

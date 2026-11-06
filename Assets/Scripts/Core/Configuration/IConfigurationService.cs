namespace Nexus.Configuration
{
    /// <summary>
    /// Access point for project configuration. Implementations may load from
    /// Resources, from a file, or return validated defaults (tests).
    /// </summary>
    public interface IConfigurationService
    {
        GameConfiguration Game { get; }

        UiTheme Theme { get; }
    }
}

namespace Nexus.Core.Platform
{
    /// <summary>
    /// Layout density profile. Presentation layers use this to pick scale and spacing
    /// without ever branching on a concrete runtime platform.
    /// </summary>
    public enum UiLayoutProfile
    {
        Compact = 0,
        Regular = 1,
        Expanded = 2
    }
}

namespace Nexus.Core.Platform
{
    /// <summary>Coarse platform classification used for settings and layout decisions.</summary>
    public enum PlatformKind
    {
        Unknown = 0,
        WindowsDesktop = 1,
        MacDesktop = 2,
        LinuxDesktop = 3,
        AndroidPhone = 4,
        AndroidTablet = 5,
        IosPhone = 6,
        IosTablet = 7
    }
}

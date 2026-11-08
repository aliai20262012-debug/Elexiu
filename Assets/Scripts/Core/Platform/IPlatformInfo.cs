namespace Nexus.Core.Platform
{
    /// <summary>Read-only description of the device the application runs on.</summary>
    public interface IPlatformInfo
    {
        PlatformKind Kind { get; }

        bool IsMobile { get; }

        bool IsTablet { get; }

        /// <summary>False on mobile platforms where "Exit application" is not an expected affordance.</summary>
        bool SupportsQuit { get; }

        float ScreenDpi { get; }

        float ScreenDiagonalInches { get; }

        UiLayoutProfile Layout { get; }
    }
}

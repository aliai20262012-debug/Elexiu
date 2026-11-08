using UnityEngine;

namespace Nexus.Core.Platform
{
    /// <summary>
    /// Runtime platform detection. The classification rules are pure functions so they
    /// can be unit tested without a device.
    /// </summary>
    public sealed class PlatformInfo : IPlatformInfo
    {
        private const float TabletWidthThresholdInches = 6.2f;

        public PlatformKind Kind { get; }

        public bool IsMobile { get; }

        public bool IsTablet { get; }

        public bool SupportsQuit { get; }

        public float ScreenDpi { get; }

        public float ScreenDiagonalInches { get; }

        public UiLayoutProfile Layout { get; }

        public PlatformInfo(PlatformKind kind, float screenDpi, float screenDiagonalInches)
        {
            Kind = kind;
            ScreenDpi = screenDpi;
            ScreenDiagonalInches = screenDiagonalInches;
            IsMobile = IsMobileKind(kind);
            IsTablet = kind == PlatformKind.AndroidTablet || kind == PlatformKind.IosTablet;
            SupportsQuit = !IsMobile;
            Layout = ResolveLayout(kind);
        }

        public static PlatformInfo Detect()
        {
            float dpi = Screen.dpi;
            float widthInches = dpi > 0f ? Screen.width / dpi : 0f;
            float heightInches = dpi > 0f ? Screen.height / dpi : 0f;
            float diagonalInches = (widthInches > 0f && heightInches > 0f)
                ? Mathf.Sqrt((widthInches * widthInches) + (heightInches * heightInches))
                : 0f;

            UnityEngine.DeviceType deviceType = UnityEngine.DeviceType.Unknown;
            try
            {
                deviceType = UnityEngine.Device.SystemInfo.deviceType;
            }
            catch (System.Exception)
            {
                // Diagnostics only: classification falls back to the width heuristic.
            }

            PlatformKind kind = Classify(Application.platform, deviceType, widthInches);
            return new PlatformInfo(kind, dpi, diagonalInches);
        }

        /// <summary>Pure classification used by <see cref="Detect"/> and by unit tests.</summary>
        public static PlatformKind Classify(RuntimePlatform runtimePlatform, UnityEngine.DeviceType deviceType, float screenWidthInches)
        {
            switch (runtimePlatform)
            {
                case RuntimePlatform.WindowsPlayer:
                case RuntimePlatform.WindowsEditor:
                    return PlatformKind.WindowsDesktop;

                case RuntimePlatform.OSXPlayer:
                case RuntimePlatform.OSXEditor:
                    return PlatformKind.MacDesktop;

                case RuntimePlatform.LinuxPlayer:
                case RuntimePlatform.LinuxEditor:
                    return PlatformKind.LinuxDesktop;

                case RuntimePlatform.Android:
                    return IsTabletSized(deviceType, screenWidthInches) ? PlatformKind.AndroidTablet : PlatformKind.AndroidPhone;

                case RuntimePlatform.IPhonePlayer:
                    return IsTabletSized(deviceType, screenWidthInches) ? PlatformKind.IosTablet : PlatformKind.IosPhone;

                default:
                    return PlatformKind.Unknown;
            }
        }

        public static bool IsMobileKind(PlatformKind kind)
        {
            return kind == PlatformKind.AndroidPhone
                   || kind == PlatformKind.AndroidTablet
                   || kind == PlatformKind.IosPhone
                   || kind == PlatformKind.IosTablet;
        }

        /// <summary>
        /// UnityEngine.DeviceType does not distinguish phones from tablets (Handheld covers both),
        /// so the physical width of the screen is used as the tie breaker.
        /// </summary>
        private static bool IsTabletSized(UnityEngine.DeviceType deviceType, float screenWidthInches)
        {
            if (deviceType == UnityEngine.DeviceType.Console)
            {
                return false;
            }

            return screenWidthInches >= TabletWidthThresholdInches;
        }

        private static UiLayoutProfile ResolveLayout(PlatformKind kind)
        {
            switch (kind)
            {
                case PlatformKind.AndroidPhone:
                case PlatformKind.IosPhone:
                    return UiLayoutProfile.Compact;

                case PlatformKind.AndroidTablet:
                case PlatformKind.IosTablet:
                    return UiLayoutProfile.Regular;

                default:
                    return UiLayoutProfile.Expanded;
            }
        }

        public override string ToString()
        {
            return Kind + " (dpi=" + ScreenDpi.ToString("0.#") + ", diagonal=" + ScreenDiagonalInches.ToString("0.##") + "in)";
        }
    }
}

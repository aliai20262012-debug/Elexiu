namespace Nexus.Core.Platform
{
    /// <summary>
    /// Scalar budget derived from <see cref="IPlatformInfo"/>. Keeping these numbers in one
    /// place means new targets (iOS, consoles) only need one mapping entry, not code changes
    /// spread across the project.
    /// </summary>
    public sealed class PlatformConfiguration
    {
        public PlatformKind Kind { get; }

        public int TargetFrameRate { get; }

        public int RecommendedQualityLevel { get; }

        public UiLayoutProfile Layout { get; }

        public bool UseHighQualityRendering { get; }

        public float SimulationTickIntervalMultiplier { get; }

        public PlatformConfiguration(
            PlatformKind kind,
            int targetFrameRate,
            int recommendedQualityLevel,
            UiLayoutProfile layout,
            bool useHighQualityRendering,
            float simulationTickIntervalMultiplier)
        {
            Kind = kind;
            TargetFrameRate = targetFrameRate;
            RecommendedQualityLevel = recommendedQualityLevel;
            Layout = layout;
            UseHighQualityRendering = useHighQualityRendering;
            SimulationTickIntervalMultiplier = simulationTickIntervalMultiplier;
        }

        /// <summary>
        /// Quality level indices match ProjectSettings/QualitySettings.asset:
        /// 0 = Mobile, 1 = PC.
        /// </summary>
        public static PlatformConfiguration For(IPlatformInfo platform)
        {
            if (platform == null)
            {
                return Desktop();
            }

            switch (platform.Kind)
            {
                case PlatformKind.AndroidPhone:
                case PlatformKind.IosPhone:
                    return new PlatformConfiguration(platform.Kind, 60, 0, UiLayoutProfile.Compact, false, 1f);

                case PlatformKind.AndroidTablet:
                case PlatformKind.IosTablet:
                    return new PlatformConfiguration(platform.Kind, 60, 0, UiLayoutProfile.Regular, true, 1f);

                default:
                    return Desktop();
            }
        }

        private static PlatformConfiguration Desktop()
        {
            return new PlatformConfiguration(PlatformKind.WindowsDesktop, 120, 1, UiLayoutProfile.Expanded, true, 1f);
        }

        public override string ToString()
        {
            return Kind + " target " + TargetFrameRate + "fps, quality index " + RecommendedQualityLevel;
        }
    }
}

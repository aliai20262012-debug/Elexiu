using UnityEngine;

namespace Nexus.Planet
{
    /// <summary>Describes the two camera framing states of the planet view.</summary>
    public enum PlanetViewMode
    {
        /// <summary>Whole planet, free orbit and zoom.</summary>
        Overview = 0,

        /// <summary>Close orbit around a selected region.</summary>
        RegionFocus = 1
    }

    /// <summary>
    /// Camera tuning, expressed in multiples of the planet radius so the same numbers work at
    /// any scene scale. Immutable and validated on construction.
    /// </summary>
    public readonly struct PlanetCameraSettings
    {
        public readonly float OverviewDistanceFactor;
        public readonly float FocusDistanceFactor;
        public readonly float MinDistanceFactor;
        public readonly float MaxDistanceFactor;
        public readonly float OrbitDegreesPerPixel;
        public readonly float ZoomStep;
        public readonly float Damping;
        public readonly float MinPitchDegrees;
        public readonly float MaxPitchDegrees;

        public PlanetCameraSettings(
            float overviewDistanceFactor,
            float focusDistanceFactor,
            float minDistanceFactor,
            float maxDistanceFactor,
            float orbitDegreesPerPixel,
            float zoomStep,
            float damping,
            float minPitchDegrees,
            float maxPitchDegrees)
        {
            MinDistanceFactor = Mathf.Clamp(minDistanceFactor, 1.05f, 4f);
            MaxDistanceFactor = Mathf.Clamp(maxDistanceFactor, MinDistanceFactor + 0.5f, 20f);
            OverviewDistanceFactor = Mathf.Clamp(overviewDistanceFactor, MinDistanceFactor, MaxDistanceFactor);
            FocusDistanceFactor = Mathf.Clamp(focusDistanceFactor, MinDistanceFactor, MaxDistanceFactor);
            OrbitDegreesPerPixel = Mathf.Clamp(orbitDegreesPerPixel, 0.02f, 1.5f);
            ZoomStep = Mathf.Clamp(zoomStep, 0.02f, 0.6f);
            Damping = Mathf.Clamp(damping, 0.5f, 40f);
            MinPitchDegrees = Mathf.Clamp(minPitchDegrees, -89f, 0f);
            MaxPitchDegrees = Mathf.Clamp(maxPitchDegrees, 0f, 89f);
        }

        /// <summary>Desktop and tablet framing.</summary>
        public static PlanetCameraSettings Default
        {
            get { return new PlanetCameraSettings(3.4f, 2.05f, 1.35f, 6.2f, 0.22f, 0.12f, 8f, -78f, 78f); }
        }

        /// <summary>Phone framing: slightly wider, slower and less sensitive.</summary>
        public static PlanetCameraSettings Compact
        {
            get { return new PlanetCameraSettings(3.8f, 2.3f, 1.35f, 7f, 0.18f, 0.1f, 7f, -72f, 72f); }
        }

        /// <summary>Overview camera distance for a planet of <paramref name="radius"/> units.</summary>
        public float OverviewDistance(float radius)
        {
            return radius * OverviewDistanceFactor;
        }

        public float FocusDistance(float radius)
        {
            return radius * FocusDistanceFactor;
        }

        public float MinDistance(float radius)
        {
            return radius * MinDistanceFactor;
        }

        public float MaxDistance(float radius)
        {
            return radius * MaxDistanceFactor;
        }
    }
}

using UnityEngine;

namespace Nexus.Planet
{
    /// <summary>
    /// Immutable surface generation parameters. Derived from <see cref="PlanetDefinition"/>
    /// plus the session seed, so the simulation systems and the renderer build exactly the
    /// same planet from the same numbers.
    /// </summary>
    public readonly struct PlanetSurfaceSettings
    {
        public readonly int Seed;
        public readonly float SeaLevel;
        public readonly float ContinentFrequency;
        public readonly float DetailFrequency;
        public readonly float RidgeStrength;
        public readonly float Relief;
        public readonly float OceanDepth;

        public PlanetSurfaceSettings(
            int seed,
            float seaLevel,
            float continentFrequency,
            float detailFrequency,
            float ridgeStrength,
            float relief,
            float oceanDepth)
        {
            Seed = seed;
            SeaLevel = Mathf.Clamp(seaLevel, 0.05f, 0.95f);
            ContinentFrequency = Mathf.Clamp(continentFrequency, 0.2f, 6f);
            DetailFrequency = Mathf.Clamp(detailFrequency, 1f, 32f);
            RidgeStrength = Mathf.Clamp(ridgeStrength, 0f, 1f);
            Relief = Mathf.Clamp(relief, 0.002f, 0.15f);
            OceanDepth = Mathf.Clamp(oceanDepth, 0f, 0.08f);
        }

        /// <summary>Builds settings from an authored definition and a session seed.</summary>
        public static PlanetSurfaceSettings FromDefinition(PlanetDefinition definition, int sessionSeed)
        {
            if (definition == null)
            {
                return new PlanetSurfaceSettings(sessionSeed, 0.5f, 1.35f, 7.5f, 0.55f, 0.03f, 0.012f);
            }

            return new PlanetSurfaceSettings(
                definition.ResolveSurfaceSeed(sessionSeed),
                definition.SeaLevel,
                definition.ContinentFrequency,
                definition.DetailFrequency,
                definition.RidgeStrength,
                definition.SurfaceRelief,
                definition.OceanDepth);
        }
    }
}

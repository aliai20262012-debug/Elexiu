using System.Collections.Generic;
using UnityEngine;

namespace Nexus.Planet
{
    /// <summary>
    /// Procedural surface model of a planet: a deterministic height field plus the region
    /// lookup and the palette that turns both into colours. It owns no Unity objects, so the
    /// mesh builder, the texture baker, the simulation system and the tests all sample the
    /// same surface.
    /// </summary>
    public sealed class PlanetSurface
    {
        public const int DetailOctaves = 3;

        private static readonly Color DeepOcean = new Color(0.016f, 0.075f, 0.180f, 1f);
        private static readonly Color ShallowOcean = new Color(0.063f, 0.345f, 0.451f, 1f);
        private static readonly Color PolarIce = new Color(0.800f, 0.878f, 0.925f, 1f);
        private static readonly Color Snow = new Color(0.878f, 0.925f, 0.949f, 1f);

        private readonly PlanetSurfaceSettings _settings;
        private readonly IReadOnlyList<PlanetRegion> _regions;

        public PlanetSurface(PlanetSurfaceSettings settings, IReadOnlyList<PlanetRegion> regions)
        {
            _settings = settings;
            _regions = regions ?? new PlanetRegion[0];
        }

        public PlanetSurfaceSettings Settings
        {
            get { return _settings; }
        }

        public IReadOnlyList<PlanetRegion> Regions
        {
            get { return _regions; }
        }

        public float SeaLevel
        {
            get { return _settings.SeaLevel; }
        }

        /// <summary>Normalised height (0..1); land starts at <see cref="SeaLevel"/>.</summary>
        public float HeightAt(Vector3 direction)
        {
            return HeightAt(direction, DetailOctaves);
        }

        /// <summary>
        /// Height with a reduced octave count, used by the texture baker to keep load times
        /// affordable on mobile without changing the large shapes.
        /// </summary>
        public float HeightAt(Vector3 direction, int octaves)
        {
            if (!PlanetGeo.IsUsableDirection(direction))
            {
                return _settings.SeaLevel;
            }

            Vector3 p = direction.normalized;
            int baseSeed = _settings.Seed;

            float continents = PlanetNoise.Fbm(p * _settings.ContinentFrequency, baseSeed, 4);
            float detail = octaves > 1
                ? PlanetNoise.Fbm(p * _settings.DetailFrequency, baseSeed + 101, octaves)
                : 0.5f;
            float ridges = _settings.RidgeStrength > 0f && octaves > 0
                ? PlanetNoise.RidgedFbm(p * (_settings.DetailFrequency * 1.7f), baseSeed + 211, Mathf.Max(1, octaves - 1))
                : 0f;

            float shaped = (continents * 0.80f) + (detail * 0.12f) + (ridges * _settings.RidgeStrength * 0.08f);

            // Push the field towards the extremes so coastlines read as coastlines instead of
            // a uniform grey band.
            shaped = SmoothStep(0f, 1f, Mathf.Clamp01(shaped));
            shaped = SmoothStep(0f, 1f, shaped);
            return Mathf.Clamp01(shaped);
        }

        public bool IsLand(Vector3 direction)
        {
            return HeightAt(direction) >= _settings.SeaLevel;
        }

        /// <summary>Land test for an already sampled height (avoids a second noise lookup).</summary>
        public bool IsLand(Vector3 direction, float height)
        {
            return height >= _settings.SeaLevel;
        }

        /// <summary>0 at the coastline, rising to 1 at the highest peak.</summary>
        public float LandElevation(Vector3 direction)
        {
            float height = HeightAt(direction);
            float above = height - _settings.SeaLevel;
            float range = Mathf.Max(0.0001f, 1f - _settings.SeaLevel);
            return Mathf.Clamp01(above / range);
        }

        /// <summary>0 at the coastline, rising to 1 at the deepest trench.</summary>
        public float WaterDepth(Vector3 direction)
        {
            float height = HeightAt(direction);
            float below = _settings.SeaLevel - height;
            float range = Mathf.Max(0.0001f, _settings.SeaLevel);
            return Mathf.Clamp01(below / range);
        }

        public PlanetRegion RegionAt(Vector3 direction)
        {
            PlanetRegion region;
            return RegionLocator.TryFindRegion(_regions, direction, out region) ? region : null;
        }

        public int RegionIdAt(Vector3 direction)
        {
            return RegionLocator.FindRegionId(_regions, direction);
        }

        /// <summary>
        /// Region nearest to a direction, ignoring region radii. Used for picking and tint so
        /// the whole planet is covered by an identifiable zone, while the authored radius still
        /// controls how much of it the highlight cap paints.
        /// </summary>
        public PlanetRegion NearestRegionAt(Vector3 direction)
        {
            int index = RegionLocator.FindNearestRegionIndex(_regions, direction);
            return index >= 0 ? _regions[index] : null;
        }

        /// <summary>Colour of the surface at a direction; samples the height itself.</summary>
        public Color AlbedoAt(Vector3 direction)
        {
            return AlbedoAt(direction, HeightAt(direction));
        }

        /// <summary>
        /// Colour for a known height; the texture baker passes heights from its shared grid so
        /// each texel does not re-sample the noise field.
        /// </summary>
        public Color AlbedoAt(Vector3 direction, float height)
        {
            Vector3 normalised = PlanetGeo.IsUsableDirection(direction) ? direction.normalized : Vector3.up;
            bool land = height >= _settings.SeaLevel;
            float polar = Mathf.Abs(normalised.y);

            if (!land)
            {
                float depth = Mathf.Clamp01((_settings.SeaLevel - height) / Mathf.Max(0.0001f, _settings.SeaLevel));
                Color water = Color.Lerp(ShallowOcean, DeepOcean, Mathf.Pow(depth, 0.55f));
                if (polar > 0.90f)
                {
                    water = Color.Lerp(water, PolarIce, Mathf.InverseLerp(0.90f, 0.99f, polar));
                }

                return water;
            }

            PlanetRegion exactRegion = RegionAt(normalised);
            PlanetRegion region = exactRegion ?? NearestRegionAt(normalised);
            RegionTerrainCategory terrain = exactRegion != null
                ? exactRegion.Terrain
                : TerrainFromElevation(LandElevation(normalised));

            Color color = TerrainColor(terrain);

            // Altitude shading: rocky high ground towards the peaks.
            float elevation = Mathf.Clamp01((height - _settings.SeaLevel) / Mathf.Max(0.0001f, 1f - _settings.SeaLevel));
            color = Color.Lerp(color, new Color(0.36f, 0.34f, 0.31f, 1f), Mathf.SmoothStep(0f, 0.65f, elevation));

            if (elevation > 0.72f || polar > 0.88f)
            {
                float snow = Mathf.Max(
                    Mathf.InverseLerp(0.72f, 0.92f, elevation),
                    Mathf.InverseLerp(0.88f, 0.98f, polar));
                color = Color.Lerp(color, Snow, snow * 0.85f);
            }

            // Subtle per-region tint keeps the regions identifiable from orbit without
            // turning the planet into a coloured pie chart. The zone outside a region's
            // authored radius gets an even lighter tint of its nearest region.
            if (region != null)
            {
                color = Color.Lerp(color, AccentFor(region), exactRegion != null ? 0.10f : 0.05f);
            }

            // Fine grain so large flat areas do not look like plastic.
            float grain = PlanetNoise.Value(normalised * 42f, _settings.Seed + 977);
            color *= 0.94f + (grain * 0.12f);
            color.a = 1f;
            return color;
        }

        /// <summary>Palette used by the surface, the legend and future map overlays.</summary>
        public static Color TerrainColor(RegionTerrainCategory terrain)
        {
            switch (terrain)
            {
                case RegionTerrainCategory.Ocean:
                    return DeepOcean;
                case RegionTerrainCategory.Coastal:
                    return new Color(0.235f, 0.396f, 0.267f, 1f);
                case RegionTerrainCategory.Plains:
                    return new Color(0.239f, 0.435f, 0.251f, 1f);
                case RegionTerrainCategory.Steppe:
                    return new Color(0.427f, 0.451f, 0.267f, 1f);
                case RegionTerrainCategory.Desert:
                    return new Color(0.702f, 0.576f, 0.337f, 1f);
                case RegionTerrainCategory.Tundra:
                    return new Color(0.451f, 0.494f, 0.463f, 1f);
                case RegionTerrainCategory.Highlands:
                    return new Color(0.365f, 0.333f, 0.275f, 1f);
                case RegionTerrainCategory.Mountains:
                    return new Color(0.427f, 0.408f, 0.388f, 1f);
                case RegionTerrainCategory.Volcanic:
                    return new Color(0.196f, 0.157f, 0.157f, 1f);
                case RegionTerrainCategory.Ice:
                    return new Color(0.812f, 0.875f, 0.918f, 1f);
                default:
                    return new Color(0.3f, 0.3f, 0.3f, 1f);
            }
        }

        /// <summary>
        /// Stable accent colour for a region: cyan-to-green identity hues, shifted slightly by
        /// the region id so neighbouring regions stay distinguishable.
        /// </summary>
        public static Color AccentFor(PlanetRegion region)
        {
            if (region == null)
            {
                return new Color(0.133f, 0.827f, 0.933f, 1f);
            }

            float hue = Mathf.Repeat(0.47f + ((region.Id * 0.11f) % 0.16f), 1f);
            float saturation = 0.55f + ((region.Id % 3) * 0.08f);
            Color accent = Color.HSVToRGB(hue, saturation, 0.95f);
            accent.a = 1f;
            return accent;
        }

        private static RegionTerrainCategory TerrainFromElevation(float elevation)
        {
            if (elevation < 0.10f)
            {
                return RegionTerrainCategory.Coastal;
            }

            if (elevation < 0.45f)
            {
                return RegionTerrainCategory.Plains;
            }

            if (elevation < 0.70f)
            {
                return RegionTerrainCategory.Highlands;
            }

            return RegionTerrainCategory.Mountains;
        }

        private static float SmoothStep(float edge0, float edge1, float value)
        {
            float t = Mathf.Clamp01((value - edge0) / Mathf.Max(0.0001f, edge1 - edge0));
            return t * t * (3f - (2f * t));
        }
    }
}

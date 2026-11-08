using System.Collections.Generic;

namespace Nexus.Planet
{
    /// <summary>
    /// Semantic validation for planet definitions and region sets. Structural problems
    /// (duplicate ids, unknown enums, out of range coordinates) are reported as issues rather
    /// than exceptions, so a designer editing the data file can never break the game loop.
    /// </summary>
    public static class PlanetDataValidator
    {
        public const int MinRegionCount = 3;
        public const int MaxRegionCount = 32;
        public const float MinAngularRadiusDegrees = 2f;
        public const float MaxAngularRadiusDegrees = 45f;

        /// <summary>Validates the definition, including its region set.</summary>
        public static PlanetValidationReport Validate(PlanetDefinition definition)
        {
            PlanetValidationReport report = new PlanetValidationReport();

            if (definition == null)
            {
                report.Add("definition", "no planet definition was supplied");
                return report;
            }

            if (string.IsNullOrEmpty(definition.PlanetId) || definition.PlanetId.Trim().Length == 0)
            {
                report.Add("identity", "planet id is empty");
            }

            if (string.IsNullOrEmpty(definition.DisplayName) || definition.DisplayName.Trim().Length == 0)
            {
                report.Add("identity", "display name is empty");
            }

            if (definition.RadiusKm < 500f || definition.RadiusKm > 20000f)
            {
                report.Add("physical", "radius must be inside 500..20000 km");
            }

            if (definition.Gravity <= 0f)
            {
                report.Add("physical", "gravity must be positive");
            }

            if (definition.DayLengthSeconds < 600f || definition.DayLengthSeconds > 1000000f)
            {
                report.Add("physical", "day length must be inside 600 s .. 1,000,000 s");
            }

            if (definition.MeshSubdivisions < 2 || definition.MeshSubdivisions > 6)
            {
                report.Add("quality", "mesh subdivisions must be inside 2..6");
            }

            if (definition.TextureResolution < 128 || definition.TextureResolution > 2048)
            {
                report.Add("quality", "texture resolution must be inside 128..2048");
            }

            ValidateRegions(definition.Regions, report);
            return report;
        }

        /// <summary>Validates a region set in isolation (used by loader tests).</summary>
        public static PlanetValidationReport ValidateRegions(IReadOnlyList<PlanetRegion> regions)
        {
            PlanetValidationReport report = new PlanetValidationReport();
            ValidateRegions(regions, report);
            return report;
        }

        private static void ValidateRegions(IReadOnlyList<PlanetRegion> regions, PlanetValidationReport report)
        {
            if (regions == null)
            {
                report.Add("regions", "region set is missing");
                return;
            }

            if (regions.Count < MinRegionCount)
            {
                report.Add("regions", "at least " + MinRegionCount + " regions are required, found " + regions.Count);
            }

            if (regions.Count > MaxRegionCount)
            {
                report.Add("regions", "at most " + MaxRegionCount + " regions are supported, found " + regions.Count);
            }

            HashSet<int> ids = new HashSet<int>();
            HashSet<string> codes = new HashSet<string>();

            for (int i = 0; i < regions.Count; i++)
            {
                PlanetRegion region = regions[i];
                if (region == null)
                {
                    report.Add("regions", "region #" + i + " is null");
                    continue;
                }

                string context = "region " + (string.IsNullOrEmpty(region.Code) ? "#" + i : region.Code);

                if (region.Id <= 0)
                {
                    report.Add(context, "id must be positive");
                }
                else if (!ids.Add(region.Id))
                {
                    report.Add(context, "duplicate id " + region.Id);
                }

                if (string.IsNullOrEmpty(region.Code))
                {
                    report.Add(context, "code is empty");
                }
                else if (region.Code.Length > 12)
                {
                    report.Add(context, "code must be 12 characters or fewer");
                }
                else if (!codes.Add(region.Code))
                {
                    report.Add(context, "duplicate code");
                }

                if (string.IsNullOrEmpty(region.Name))
                {
                    report.Add(context, "name is empty");
                }

                if (region.LatitudeDegrees < PlanetGeo.MinLatitude || region.LatitudeDegrees > PlanetGeo.MaxLatitude)
                {
                    report.Add(context, "latitude is outside -90..90");
                }

                if (region.AngularRadiusDegrees < MinAngularRadiusDegrees ||
                    region.AngularRadiusDegrees > MaxAngularRadiusDegrees)
                {
                    report.Add(context, "angular radius is outside " + MinAngularRadiusDegrees + ".." + MaxAngularRadiusDegrees + " degrees");
                }

                if (region.IsWaterRegion && region.Terrain != RegionTerrainCategory.Ocean &&
                    region.Terrain != RegionTerrainCategory.Coastal)
                {
                    report.Add(context, "a water region must use the ocean or coastal terrain category");
                }

                if (region.Resources != null)
                {
                    HashSet<ResourceType> types = new HashSet<ResourceType>();
                    for (int r = 0; r < region.Resources.Count; r++)
                    {
                        if (!types.Add(region.Resources[r].Type))
                        {
                            report.Add(context, "duplicate resource " + region.Resources[r].Type);
                        }
                    }
                }

                if (region.Habitability != null && region.Habitability.Score > 0.95f && region.Environment != null &&
                    region.Environment.Radiation > 0.6f)
                {
                    report.Add(context, "very high habitability with very high radiation is inconsistent");
                }
            }
        }
    }
}

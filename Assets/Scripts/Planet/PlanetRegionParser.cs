using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nexus.Planet
{
    /// <summary>
    /// Parses the authored region JSON into validated <see cref="PlanetRegion"/> instances.
    ///
    /// The parser is defensive by design: a broken entry is skipped or clamped and the reason
    /// is appended to the issue list. It never throws, because the region file is designer
    /// editable data that must not be able to break the game loop.
    /// </summary>
    public static class PlanetRegionParser
    {
        /// <summary>Parses <paramref name="json"/>; problems are appended to <paramref name="issues"/>.</summary>
        public static IReadOnlyList<PlanetRegion> Parse(string json, string expectedPlanetId, ICollection<string> issues)
        {
            List<PlanetRegion> regions = new List<PlanetRegion>();
            ICollection<string> report = issues ?? new List<string>();

            if (string.IsNullOrEmpty(json) || json.Trim().Length == 0)
            {
                report.Add("region file is empty");
                return regions;
            }

            PlanetRegionFile file;
            try
            {
                file = JsonUtility.FromJson<PlanetRegionFile>(json);
            }
            catch (Exception exception)
            {
                report.Add("region file is not valid JSON: " + exception.Message);
                return regions;
            }

            if (file == null || file.regions == null || file.regions.Length == 0)
            {
                report.Add("region file contains no regions");
                return regions;
            }

            if (!string.IsNullOrEmpty(expectedPlanetId) && !string.IsNullOrEmpty(file.planetId) &&
                !string.Equals(file.planetId.Trim(), expectedPlanetId.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                report.Add("region file belongs to '" + file.planetId + "' instead of '" + expectedPlanetId + "'");
            }

            HashSet<int> ids = new HashSet<int>();
            HashSet<string> codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < file.regions.Length; i++)
            {
                PlanetRegionFile.RegionRecord record = file.regions[i];
                string context = "region #" + i;

                if (record == null)
                {
                    report.Add(context + ": entry is null");
                    continue;
                }

                string code = (record.code ?? string.Empty).Trim();
                if (code.Length > 0)
                {
                    context = "region " + code;
                }

                if (record.id <= 0)
                {
                    report.Add(context + ": id must be positive");
                    continue;
                }

                if (!ids.Add(record.id))
                {
                    report.Add(context + ": duplicate id " + record.id + " skipped");
                    continue;
                }

                if (code.Length > 0 && !codes.Add(code))
                {
                    report.Add(context + ": duplicate code skipped");
                    continue;
                }

                if (float.IsNaN(record.latitude) || record.latitude < PlanetGeo.MinLatitude || record.latitude > PlanetGeo.MaxLatitude)
                {
                    report.Add(context + ": latitude " + record.latitude + " is outside -90..90, region skipped");
                    continue;
                }

                if (float.IsNaN(record.longitude) || record.longitude < PlanetGeo.MinLongitude || record.longitude > PlanetGeo.MaxLongitude)
                {
                    report.Add(context + ": longitude " + record.longitude + " is outside -180..180, wrapped");
                }

                float radius = record.radius;
                if (float.IsNaN(radius) || radius < PlanetDataValidator.MinAngularRadiusDegrees ||
                    radius > PlanetDataValidator.MaxAngularRadiusDegrees)
                {
                    float clamped = Mathf.Clamp(float.IsNaN(radius) ? 12f : radius,
                        PlanetDataValidator.MinAngularRadiusDegrees,
                        PlanetDataValidator.MaxAngularRadiusDegrees);
                    report.Add(context + ": angular radius clamped to " + clamped + " degrees");
                    radius = clamped;
                }

                float landFraction = record.landFraction;
                if (float.IsNaN(landFraction) || landFraction < 0f || landFraction > 1f)
                {
                    report.Add(context + ": land fraction clamped to 0..1");
                    landFraction = Mathf.Clamp(float.IsNaN(landFraction) ? 0f : landFraction, 0f, 1f);
                }

                RegionTerrainCategory terrain;
                if (!TryParseEnum(record.terrain, RegionTerrainCategory.Plains, out terrain))
                {
                    report.Add(context + ": unknown terrain '" + record.terrain + "', using PLAINS");
                }

                WaterClassification water;
                if (!TryParseEnum(record.water, WaterClassification.Coastal, out water))
                {
                    report.Add(context + ": unknown water classification '" + record.water + "', using COASTAL");
                }

                ClimateProfile climate = BuildClimate(record.climate, context, report);
                EnvironmentalIndicators environment = BuildEnvironment(record.environment);
                HabitabilityProfile habitability = BuildHabitability(record.habitability);
                ResourceDeposit[] resources = BuildResources(record.resources, context, report);

                string name = (record.name ?? string.Empty).Trim();
                if (name.Length == 0)
                {
                    name = code.Length > 0 ? code : "REGION " + record.id;
                    report.Add(context + ": missing name, using '" + name + "'");
                }

                regions.Add(new PlanetRegion(
                    record.id,
                    code,
                    name,
                    record.description,
                    terrain,
                    water,
                    landFraction,
                    record.latitude,
                    record.longitude,
                    radius,
                    climate,
                    resources,
                    environment,
                    habitability));
            }

            return regions;
        }

        private static ClimateProfile BuildClimate(
            PlanetRegionFile.ClimateRecord record,
            string context,
            ICollection<string> report)
        {
            if (record == null)
            {
                report.Add(context + ": missing climate block, using neutral defaults");
                return new ClimateProfile(10f, 20f, 0.5f, 15f, 600f);
            }

            if (record.meanTemperatureC < ClimateProfile.MinTemperatureC || record.meanTemperatureC > ClimateProfile.MaxTemperatureC)
            {
                report.Add(context + ": mean temperature clamped");
            }

            if (record.humidity < 0f || record.humidity > 1f)
            {
                report.Add(context + ": humidity clamped to 0..1");
            }

            return new ClimateProfile(
                record.meanTemperatureC,
                record.temperatureRangeC,
                record.humidity,
                record.windKph,
                record.precipitationMm);
        }

        private static EnvironmentalIndicators BuildEnvironment(PlanetRegionFile.EnvironmentRecord record)
        {
            if (record == null)
            {
                return EnvironmentalIndicators.Neutral;
            }

            return new EnvironmentalIndicators(
                record.airQuality,
                record.radiation,
                record.biodiversity,
                record.tectonicActivity);
        }

        private static HabitabilityProfile BuildHabitability(PlanetRegionFile.HabitabilityRecord record)
        {
            if (record == null)
            {
                return new HabitabilityProfile(0f, "UNASSESSED");
            }

            return new HabitabilityProfile(record.score, record.limitingFactor);
        }

        private static ResourceDeposit[] BuildResources(
            PlanetRegionFile.ResourceRecord[] records,
            string context,
            ICollection<string> report)
        {
            if (records == null || records.Length == 0)
            {
                return new ResourceDeposit[0];
            }

            List<ResourceDeposit> deposits = new List<ResourceDeposit>(records.Length);
            HashSet<ResourceType> seen = new HashSet<ResourceType>();

            for (int i = 0; i < records.Length; i++)
            {
                PlanetRegionFile.ResourceRecord record = records[i];
                if (record == null)
                {
                    continue;
                }

                ResourceType type;
                if (!TryParseEnum(record.type, ResourceType.Minerals, out type))
                {
                    report.Add(context + ": unknown resource '" + record.type + "', entry skipped");
                    continue;
                }

                if (!seen.Add(type))
                {
                    report.Add(context + ": duplicate resource " + type + ", entry skipped");
                    continue;
                }

                if (record.abundance < 0f || record.abundance > 1f || float.IsNaN(record.abundance))
                {
                    report.Add(context + ": " + type + " abundance clamped to 0..1");
                }

                deposits.Add(new ResourceDeposit(type, record.abundance));
            }

            return deposits.ToArray();
        }

        private static bool TryParseEnum<TEnum>(string value, TEnum fallback, out TEnum result)
            where TEnum : struct
        {
            if (!string.IsNullOrEmpty(value) && Enum.TryParse(value.Trim(), true, out result) &&
                Enum.IsDefined(typeof(TEnum), result))
            {
                return true;
            }

            result = fallback;
            return false;
        }
    }
}

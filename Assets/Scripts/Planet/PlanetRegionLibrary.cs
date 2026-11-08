using System.Collections.Generic;

namespace Nexus.Planet
{
    /// <summary>
    /// Deterministic, code-authored region set for NEXUS-01. It is the fallback used when the
    /// authored region file is missing (fresh clone, stripped Resources) and the fixture used
    /// by tests that need valid data without touching the asset pipeline.
    /// </summary>
    public static class PlanetRegionLibrary
    {
        public static IReadOnlyList<PlanetRegion> CreateDefaultRegions()
        {
            List<PlanetRegion> regions = new List<PlanetRegion>(8);

            regions.Add(new PlanetRegion(
                1, "NX-AU-01", "AURORA BASIN",
                "Sheltered equatorial basin with year-round rainfall and a wide continental shelf.",
                RegionTerrainCategory.Plains, WaterClassification.Coastal,
                0.72f, 34f, -18f, 16f,
                new ClimateProfile(16.5f, 12f, 0.62f, 14f, 780f),
                new[]
                {
                    new ResourceDeposit(ResourceType.Water, 0.82f),
                    new ResourceDeposit(ResourceType.Biomass, 0.68f),
                    new ResourceDeposit(ResourceType.Minerals, 0.44f),
                    new ResourceDeposit(ResourceType.Metals, 0.31f)
                },
                new EnvironmentalIndicators(0.88f, 0.12f, 0.74f, 0.22f),
                new HabitabilityProfile(0.81f, "SEASONAL STORMS")));

            regions.Add(new PlanetRegion(
                2, "NX-VD-02", "VERDANT SHELF",
                "Warm shallow seas and mangrove deltas; the richest biomass reservoir on the planet.",
                RegionTerrainCategory.Coastal, WaterClassification.OceanAccess,
                0.61f, 12f, 42f, 15f,
                new ClimateProfile(23.8f, 7f, 0.84f, 18f, 1420f),
                new[]
                {
                    new ResourceDeposit(ResourceType.Biomass, 0.91f),
                    new ResourceDeposit(ResourceType.Water, 0.88f),
                    new ResourceDeposit(ResourceType.Minerals, 0.22f)
                },
                new EnvironmentalIndicators(0.79f, 0.18f, 0.93f, 0.14f),
                new HabitabilityProfile(0.74f, "TROPICAL DISEASE VECTORS")));

            regions.Add(new PlanetRegion(
                3, "NX-KR-03", "KARRIC HIGHLANDS",
                "Cold, mineral-rich plateaus cut by glacial valleys and old impact craters.",
                RegionTerrainCategory.Highlands, WaterClassification.Landlocked,
                0.95f, 46f, 96f, 18f,
                new ClimateProfile(2.4f, 26f, 0.38f, 34f, 310f),
                new[]
                {
                    new ResourceDeposit(ResourceType.Metals, 0.76f),
                    new ResourceDeposit(ResourceType.RareEarths, 0.58f),
                    new ResourceDeposit(ResourceType.Minerals, 0.71f),
                    new ResourceDeposit(ResourceType.Geothermal, 0.36f)
                },
                new EnvironmentalIndicators(0.66f, 0.28f, 0.21f, 0.48f),
                new HabitabilityProfile(0.52f, "THIN ATMOSPHERE")));

            regions.Add(new PlanetRegion(
                4, "NX-DS-04", "DUNE MERIDIAN",
                "A continent-sized erg of iron-stained sand crossed by dry salt basins.",
                RegionTerrainCategory.Desert, WaterClassification.Landlocked,
                0.99f, -8f, 130f, 20f,
                new ClimateProfile(38.6f, 32f, 0.09f, 22f, 40f),
                new[]
                {
                    new ResourceDeposit(ResourceType.RareEarths, 0.69f),
                    new ResourceDeposit(ResourceType.Minerals, 0.64f),
                    new ResourceDeposit(ResourceType.Metals, 0.47f)
                },
                new EnvironmentalIndicators(0.54f, 0.41f, 0.06f, 0.18f),
                new HabitabilityProfile(0.24f, "EXTREME HEAT")));

            regions.Add(new PlanetRegion(
                5, "NX-HL-05", "HALCYON TUNDRA",
                "Wind-scoured northern plain with permafrost, fjords and a short growing season.",
                RegionTerrainCategory.Tundra, WaterClassification.Coastal,
                0.78f, 62f, -120f, 17f,
                new ClimateProfile(-14.2f, 24f, 0.46f, 41f, 260f),
                new[]
                {
                    new ResourceDeposit(ResourceType.Water, 0.74f),
                    new ResourceDeposit(ResourceType.Metals, 0.52f),
                    new ResourceDeposit(ResourceType.Biomass, 0.19f)
                },
                new EnvironmentalIndicators(0.83f, 0.22f, 0.18f, 0.12f),
                new HabitabilityProfile(0.41f, "PERMAFROST")));

            regions.Add(new PlanetRegion(
                6, "NX-OC-06", "MERIDIAN ABYSS",
                "Deep ocean region with hadal trenches and persistent storm systems.",
                RegionTerrainCategory.Ocean, WaterClassification.OpenOcean,
                0.04f, -20f, -150f, 24f,
                new ClimateProfile(19.1f, 4f, 0.96f, 52f, 1810f),
                new[]
                {
                    new ResourceDeposit(ResourceType.Water, 1f),
                    new ResourceDeposit(ResourceType.Geothermal, 0.44f),
                    new ResourceDeposit(ResourceType.Biomass, 0.27f)
                },
                new EnvironmentalIndicators(0.86f, 0.16f, 0.58f, 0.62f),
                new HabitabilityProfile(0.08f, "NO LANDMASS")));

            regions.Add(new PlanetRegion(
                7, "NX-EM-07", "EMBER REACH",
                "Active volcanic archipelago; geothermal energy is abundant but the ground is unstable.",
                RegionTerrainCategory.Volcanic, WaterClassification.Coastal,
                0.38f, -28f, 60f, 12f,
                new ClimateProfile(29.4f, 11f, 0.58f, 26f, 690f),
                new[]
                {
                    new ResourceDeposit(ResourceType.Geothermal, 0.94f),
                    new ResourceDeposit(ResourceType.Metals, 0.63f),
                    new ResourceDeposit(ResourceType.Minerals, 0.55f)
                },
                new EnvironmentalIndicators(0.42f, 0.37f, 0.29f, 0.91f),
                new HabitabilityProfile(0.18f, "VOLCANIC ACTIVITY")));

            regions.Add(new PlanetRegion(
                8, "NX-IS-08", "POLAR VEIL",
                "Permanent ice sheet over a subglacial sea; scientific value exceeds settlement value.",
                RegionTerrainCategory.Ice, WaterClassification.Landlocked,
                0.66f, 82f, 10f, 10f,
                new ClimateProfile(-38.5f, 18f, 0.22f, 48f, 90f),
                new[]
                {
                    new ResourceDeposit(ResourceType.Water, 0.97f),
                    new ResourceDeposit(ResourceType.RareEarths, 0.31f)
                },
                new EnvironmentalIndicators(0.91f, 0.34f, 0.04f, 0.08f),
                new HabitabilityProfile(0.12f, "POLAR NIGHT")));

            return regions;
        }
    }
}

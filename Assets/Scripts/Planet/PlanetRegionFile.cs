using System;

namespace Nexus.Planet
{
    /// <summary>
    /// Serialisation shape of the authored region file (<c>Resources/Config/Planet/*.json</c>).
    /// It is a deliberate data-transfer layer: enums are written as readable strings,
    /// missing values are detectable (<c>null</c>) and nothing here is used at runtime once
    /// <see cref="PlanetRegionParser"/> has produced validated <see cref="PlanetRegion"/>
    /// instances.
    /// </summary>
    [Serializable]
    public sealed class PlanetRegionFile
    {
        public string planetId;
        public string schemaVersion;
        public RegionRecord[] regions;

        [Serializable]
        public sealed class RegionRecord
        {
            public int id;
            public string code;
            public string name;
            public string description;
            public string terrain;
            public string water;
            public float landFraction;
            public float latitude;
            public float longitude;
            public float radius;
            public ClimateRecord climate;
            public ResourceRecord[] resources;
            public EnvironmentRecord environment;
            public HabitabilityRecord habitability;
        }

        [Serializable]
        public sealed class ClimateRecord
        {
            public float meanTemperatureC;
            public float temperatureRangeC;
            public float humidity;
            public float windKph;
            public float precipitationMm;
        }

        [Serializable]
        public sealed class ResourceRecord
        {
            public string type;
            public float abundance;
        }

        [Serializable]
        public sealed class EnvironmentRecord
        {
            public float airQuality;
            public float radiation;
            public float biodiversity;
            public float tectonicActivity;
        }

        [Serializable]
        public sealed class HabitabilityRecord
        {
            public float score;
            public string limitingFactor;
        }
    }
}

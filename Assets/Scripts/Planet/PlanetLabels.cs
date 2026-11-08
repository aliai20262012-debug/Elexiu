using System.Globalization;

namespace Nexus.Planet
{
    /// <summary>
    /// Player-facing labels and number formatting for planet data. Kept in one place so the
    /// region panel and future codex screens never disagree on wording.
    /// </summary>
    public static class PlanetLabels
    {
        public static string Terrain(RegionTerrainCategory terrain)
        {
            switch (terrain)
            {
                case RegionTerrainCategory.Ocean:
                    return "OCEAN";
                case RegionTerrainCategory.Coastal:
                    return "COASTAL";
                case RegionTerrainCategory.Plains:
                    return "PLAINS";
                case RegionTerrainCategory.Steppe:
                    return "STEPPE";
                case RegionTerrainCategory.Desert:
                    return "DESERT";
                case RegionTerrainCategory.Tundra:
                    return "TUNDRA";
                case RegionTerrainCategory.Highlands:
                    return "HIGHLANDS";
                case RegionTerrainCategory.Mountains:
                    return "MOUNTAINS";
                case RegionTerrainCategory.Volcanic:
                    return "VOLCANIC";
                case RegionTerrainCategory.Ice:
                    return "ICE SHEET";
                default:
                    return "UNKNOWN";
            }
        }

        public static string Water(WaterClassification water)
        {
            switch (water)
            {
                case WaterClassification.Landlocked:
                    return "LANDLOCKED";
                case WaterClassification.Coastal:
                    return "COASTAL WATERS";
                case WaterClassification.OceanAccess:
                    return "OCEAN ACCESS";
                case WaterClassification.OpenOcean:
                    return "OPEN OCEAN";
                default:
                    return "UNKNOWN";
            }
        }

        public static string Resource(ResourceType type)
        {
            switch (type)
            {
                case ResourceType.Minerals:
                    return "MINERALS";
                case ResourceType.Metals:
                    return "METALS";
                case ResourceType.RareEarths:
                    return "RARE EARTHS";
                case ResourceType.Water:
                    return "WATER";
                case ResourceType.Biomass:
                    return "BIOMASS";
                case ResourceType.Geothermal:
                    return "GEOTHERMAL";
                default:
                    return "UNKNOWN";
            }
        }

        /// <summary>"12.4 °C" style temperature.</summary>
        public static string Temperature(float celsius)
        {
            return celsius.ToString("0.0", CultureInfo.InvariantCulture) + " °C";
        }

        /// <summary>0..1 indicator as a percentage.</summary>
        public static string Percent(float value)
        {
            float clamped = value < 0f ? 0f : (value > 1f ? 1f : value);
            return ((int)(clamped * 100f)).ToString(CultureInfo.InvariantCulture) + "%";
        }

        public static string KilometresPerHour(float value)
        {
            return ((int)value).ToString(CultureInfo.InvariantCulture) + " KM/H";
        }

        public static string Millimetres(float value)
        {
            return ((int)value).ToString(CultureInfo.InvariantCulture) + " MM";
        }

        /// <summary>"34.2° N" / "18.0° W" style coordinate.</summary>
        public static string Coordinate(float latitudeDegrees, float longitudeDegrees)
        {
            string latitude = System.Math.Abs(latitudeDegrees).ToString("0.0", CultureInfo.InvariantCulture) +
                              (latitudeDegrees >= 0f ? "° N" : "° S");
            string longitude = System.Math.Abs(longitudeDegrees).ToString("0.0", CultureInfo.InvariantCulture) +
                               (longitudeDegrees >= 0f ? "° E" : "° W");
            return latitude + "  " + longitude;
        }

        /// <summary>Simulated planet days as "DAY 012".</summary>
        public static string PlanetDay(double days)
        {
            if (days < 0.0)
            {
                days = 0.0;
            }

            return "DAY " + ((long)days).ToString("000", CultureInfo.InvariantCulture);
        }

        public static string Season(double phase)
        {
            double normalised = phase - System.Math.Floor(phase);
            if (normalised < 0.125 || normalised >= 0.875)
            {
                return "NORTHERN SUMMER";
            }

            if (normalised < 0.375)
            {
                return "AUTUMN";
            }

            if (normalised < 0.625)
            {
                return "NORTHERN WINTER";
            }

            return "SPRING";
        }
    }
}

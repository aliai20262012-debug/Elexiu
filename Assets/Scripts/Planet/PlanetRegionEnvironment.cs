namespace Nexus.Planet
{
    /// <summary>
    /// Live environmental reading for one region at a given simulation time. Produced by
    /// <see cref="PlanetSimulationState"/> and read by the region panel and future gameplay
    /// systems; it is never stored back into the authored <see cref="PlanetRegion"/>.
    /// </summary>
    public readonly struct PlanetRegionEnvironment
    {
        public readonly int RegionId;
        public readonly float TemperatureC;
        public readonly float Humidity;
        public readonly float HabitabilityScore;
        public readonly bool IsDaylight;

        public PlanetRegionEnvironment(int regionId, float temperatureC, float humidity, float habitabilityScore, bool isDaylight)
        {
            RegionId = regionId;
            TemperatureC = temperatureC;
            Humidity = humidity;
            HabitabilityScore = habitabilityScore;
            IsDaylight = isDaylight;
        }

        public override string ToString()
        {
            return "region " + RegionId + " " + TemperatureC.ToString("0.0") + "°C";
        }
    }
}

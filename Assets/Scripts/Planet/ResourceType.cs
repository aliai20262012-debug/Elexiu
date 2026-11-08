namespace Nexus.Planet
{
    /// <summary>
    /// Natural resource families tracked per region. Phase 03 only stores availability; the
    /// extractive economy that consumes it is a later phase.
    /// </summary>
    public enum ResourceType
    {
        Minerals = 0,
        Metals = 1,
        RareEarths = 2,
        Water = 3,
        Biomass = 4,
        Geothermal = 5
    }
}

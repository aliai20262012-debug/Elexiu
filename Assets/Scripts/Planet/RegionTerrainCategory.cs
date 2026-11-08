namespace Nexus.Planet
{
    /// <summary>
    /// Coarse terrain classification for a region. It drives the surface palette, the region
    /// information panel and the modifiers future gameplay systems (settlement, economy)
    /// will read from the same data.
    /// </summary>
    public enum RegionTerrainCategory
    {
        Ocean = 0,
        Coastal = 1,
        Plains = 2,
        Steppe = 3,
        Desert = 4,
        Tundra = 5,
        Highlands = 6,
        Mountains = 7,
        Volcanic = 8,
        Ice = 9
    }
}

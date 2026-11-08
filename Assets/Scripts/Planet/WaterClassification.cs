namespace Nexus.Planet
{
    /// <summary>
    /// How a region relates to the planet's water bodies. Combined with
    /// <see cref="PlanetRegion.LandFraction"/> it classifies land and water without needing a
    /// second terrain enum.
    /// </summary>
    public enum WaterClassification
    {
        /// <summary>No direct access to a sea; lakes and rivers only.</summary>
        Landlocked = 0,

        /// <summary>Coastline along a shelf sea or inland sea.</summary>
        Coastal = 1,

        /// <summary>Borders an ocean and holds a usable harbour.</summary>
        OceanAccess = 2,

        /// <summary>Mostly or entirely water (open ocean region).</summary>
        OpenOcean = 3
    }
}

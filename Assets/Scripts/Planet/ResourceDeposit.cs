using System;

namespace Nexus.Planet
{
    /// <summary>
    /// Availability of one resource family inside a region. <see cref="Abundance"/> is a
    /// normalised 0..1 indicator (0 = traces, 1 = exceptionally rich), not a quantity: the
    /// extractive economy will scale it into real units later.
    /// </summary>
    [Serializable]
    public struct ResourceDeposit
    {
        public ResourceType Type;

        public float Abundance;

        public ResourceDeposit(ResourceType type, float abundance)
        {
            Type = type;
            Abundance = Clamp01(abundance);
        }

        /// <summary>Human readable 0-100 percentage used by the region panel.</summary>
        public int AbundancePercent
        {
            get { return (int)Math.Round(Abundance * 100f); }
        }

        public override string ToString()
        {
            return Type + " " + AbundancePercent + "%";
        }

        private static float Clamp01(float value)
        {
            if (float.IsNaN(value))
            {
                return 0f;
            }

            if (value < 0f)
            {
                return 0f;
            }

            return value > 1f ? 1f : value;
        }
    }
}

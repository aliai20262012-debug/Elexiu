using System;

namespace Nexus.Planet
{
    /// <summary>
    /// Environmental readouts for a region, all normalised to 0..1. They are authored data
    /// for now and become inputs for the future environment and civilization systems.
    ///
    /// Interpretation: air quality and biodiversity are "higher is better"; radiation and
    /// tectonic activity are "lower is better".
    /// </summary>
    [Serializable]
    public sealed class EnvironmentalIndicators
    {
        private readonly float _airQuality;
        private readonly float _radiation;
        private readonly float _biodiversity;
        private readonly float _tectonicActivity;

        public EnvironmentalIndicators(float airQuality, float radiation, float biodiversity, float tectonicActivity)
        {
            _airQuality = Clamp01(airQuality);
            _radiation = Clamp01(radiation);
            _biodiversity = Clamp01(biodiversity);
            _tectonicActivity = Clamp01(tectonicActivity);
        }

        public float AirQuality
        {
            get { return _airQuality; }
        }

        public float Radiation
        {
            get { return _radiation; }
        }

        public float Biodiversity
        {
            get { return _biodiversity; }
        }

        public float TectonicActivity
        {
            get { return _tectonicActivity; }
        }

        /// <summary>Neutral environment used when a value is missing from the data file.</summary>
        public static EnvironmentalIndicators Neutral
        {
            get { return new EnvironmentalIndicators(0.5f, 0.5f, 0.5f, 0.5f); }
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

using System;

namespace Nexus.Planet
{
    /// <summary>
    /// Static climate envelope for a region. These are the baseline values authored in data;
    /// <see cref="PlanetSystem"/> derives a live temperature from them as the planet orbits
    /// and tilts, without ever mutating this profile.
    /// </summary>
    [Serializable]
    public sealed class ClimateProfile
    {
        public const float MinTemperatureC = -120f;
        public const float MaxTemperatureC = 80f;
        public const float MaxTemperatureRangeC = 120f;
        public const float MaxWindKph = 400f;
        public const float MaxPrecipitationMm = 10000f;

        private readonly float _meanTemperatureC;
        private readonly float _temperatureRangeC;
        private readonly float _humidity;
        private readonly float _windKph;
        private readonly float _precipitationMm;

        public ClimateProfile(
            float meanTemperatureC,
            float temperatureRangeC,
            float humidity,
            float windKph,
            float precipitationMm)
        {
            _meanTemperatureC = Clamp(meanTemperatureC, MinTemperatureC, MaxTemperatureC);
            _temperatureRangeC = Clamp(temperatureRangeC, 0f, MaxTemperatureRangeC);
            _humidity = Clamp(humidity, 0f, 1f);
            _windKph = Clamp(windKph, 0f, MaxWindKph);
            _precipitationMm = Clamp(precipitationMm, 0f, MaxPrecipitationMm);
        }

        /// <summary>Annual mean temperature in degrees Celsius.</summary>
        public float MeanTemperatureC
        {
            get { return _meanTemperatureC; }
        }

        /// <summary>Spread between the coldest and warmest season in degrees Celsius.</summary>
        public float TemperatureRangeC
        {
            get { return _temperatureRangeC; }
        }

        /// <summary>Relative humidity, 0..1.</summary>
        public float Humidity
        {
            get { return _humidity; }
        }

        /// <summary>Mean surface wind speed in km/h.</summary>
        public float WindKph
        {
            get { return _windKph; }
        }

        /// <summary>Annual precipitation in millimetres.</summary>
        public float PrecipitationMm
        {
            get { return _precipitationMm; }
        }

        /// <summary>True for the arid end of the spectrum (used by the region panel).</summary>
        public bool IsArid
        {
            get { return _humidity < 0.25f; }
        }

        /// <summary>
        /// Temperature for a given season phase where 0 = northern summer peak and
        /// 0.5 = northern winter. Pure function, deterministic across platforms.
        /// </summary>
        public float TemperatureAt(float seasonPhase)
        {
            float phase = seasonPhase - (float)Math.Floor(seasonPhase);
            float offset = (float)Math.Cos(phase * Math.PI * 2.0) * (_temperatureRangeC * 0.5f);
            float temperature = _meanTemperatureC + offset;
            return temperature > MaxTemperatureC ? MaxTemperatureC : temperature;
        }

        private static float Clamp(float value, float min, float max)
        {
            if (float.IsNaN(value))
            {
                return min;
            }

            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }
    }
}

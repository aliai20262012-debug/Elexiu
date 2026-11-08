using System;

namespace Nexus.Planet
{
    /// <summary>
    /// How habitable a region is for the player's civilization. The score is authored per
    /// region and kept independent from the live environment so designers stay in control;
    /// <see cref="PlanetSystem"/> uses it to derive colonisation pressure later.
    /// </summary>
    [Serializable]
    public sealed class HabitabilityProfile
    {
        private readonly float _score;
        private readonly string _limitingFactor;

        public HabitabilityProfile(float score, string limitingFactor)
        {
            _score = Clamp01(score);
            _limitingFactor = string.IsNullOrEmpty(limitingFactor)
                ? "NONE"
                : limitingFactor.Trim().ToUpperInvariant();
        }

        /// <summary>0 = uninhabitable, 1 = ideal.</summary>
        public float Score
        {
            get { return _score; }
        }

        /// <summary>Main constraint shown to the player (storms, radiation, cold, ...).</summary>
        public string LimitingFactor
        {
            get { return _limitingFactor; }
        }

        /// <summary>Score as a 0-100 percentage for presentation.</summary>
        public int ScorePercent
        {
            get { return (int)Math.Round(_score * 100f); }
        }

        /// <summary>Coarse rating used for the region panel badge.</summary>
        public string Rating
        {
            get
            {
                if (_score >= 0.75f)
                {
                    return "PRIORITY";
                }

                if (_score >= 0.5f)
                {
                    return "VIABLE";
                }

                if (_score >= 0.25f)
                {
                    return "MARGINAL";
                }

                return "HOSTILE";
            }
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

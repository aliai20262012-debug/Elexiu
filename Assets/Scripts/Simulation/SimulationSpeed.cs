using System;
using System.Collections.Generic;
using System.Globalization;

namespace Nexus.Simulation
{
    /// <summary>
    /// Speed multiplier rules for the simulation clock. Multipliers are pure numbers:
    /// the clock converts them into simulated seconds, so a speed change never depends
    /// on the frame rate. All helpers are static and side effect free so they can be
    /// unit tested in isolation.
    /// </summary>
    public static class SimulationSpeed
    {
        /// <summary>Real time multiplier used when the player selects the default speed.</summary>
        public const double Normal = 1.0;

        /// <summary>Slowest accepted multiplier (quarter speed).</summary>
        public const double Minimum = 0.25;

        /// <summary>Fastest accepted multiplier. Keeps a single frame from simulating hours.</summary>
        public const double Maximum = 100.0;

        private static readonly double[] FallbackLevels = { 1.0, 2.0, 4.0 };

        /// <summary>True when the multiplier is finite and inside the supported range.</summary>
        public static bool IsValid(double multiplier)
        {
            if (double.IsNaN(multiplier) || double.IsInfinity(multiplier))
            {
                return false;
            }

            return multiplier >= Minimum && multiplier <= Maximum;
        }

        /// <summary>
        /// Maps any input (including NaN, infinities and negative values) onto a usable
        /// multiplier. Never throws, so configuration mistakes degrade instead of failing.
        /// </summary>
        public static double Clamp(double multiplier)
        {
            if (double.IsNaN(multiplier))
            {
                return Normal;
            }

            if (multiplier < Minimum)
            {
                return Minimum;
            }

            if (multiplier > Maximum)
            {
                return Maximum;
            }

            return multiplier;
        }

        /// <summary>
        /// Filters, clamps, sorts and de-duplicates the configured speed levels.
        /// Returns the built-in 1x / 2x / 4x ladder when nothing usable is configured.
        /// </summary>
        public static double[] SanitizeLevels(IReadOnlyList<double> levels)
        {
            if (levels == null || levels.Count == 0)
            {
                return (double[])FallbackLevels.Clone();
            }

            List<double> valid = new List<double>(levels.Count);
            for (int i = 0; i < levels.Count; i++)
            {
                if (IsValid(levels[i]))
                {
                    valid.Add(levels[i]);
                }
            }

            if (valid.Count == 0)
            {
                return (double[])FallbackLevels.Clone();
            }

            valid.Sort();

            List<double> unique = new List<double>(valid.Count);
            for (int i = 0; i < valid.Count; i++)
            {
                if (unique.Count == 0 || Math.Abs(unique[unique.Count - 1] - valid[i]) > 0.0001)
                {
                    unique.Add(valid[i]);
                }
            }

            return unique.ToArray();
        }

        /// <summary>Returns the configured level closest to the requested multiplier.</summary>
        public static double NearestLevel(IReadOnlyList<double> levels, double preferred)
        {
            if (levels == null || levels.Count == 0)
            {
                return Normal;
            }

            double target = Clamp(preferred);
            double best = levels[0];
            double bestDelta = Math.Abs(levels[0] - target);

            for (int i = 1; i < levels.Count; i++)
            {
                double delta = Math.Abs(levels[i] - target);
                if (delta < bestDelta)
                {
                    best = levels[i];
                    bestDelta = delta;
                }
            }

            return best;
        }

        /// <summary>Stable, culture independent label ("1x", "2.5x") for the HUD.</summary>
        public static string FormatLabel(double multiplier)
        {
            return Clamp(multiplier).ToString("0.##", CultureInfo.InvariantCulture) + "x";
        }
    }
}

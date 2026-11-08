using System;
using System.Collections.Generic;
using Nexus.Configuration;

namespace Nexus.Simulation
{
    /// <summary>
    /// Validated, immutable timing configuration for a simulation session. Every value is
    /// normalised in the constructor, so an instance can never hold a negative interval,
    /// a zero catch-up budget or an empty speed ladder. That makes invalid designer input
    /// a clamped setting rather than a runtime failure.
    /// </summary>
    public readonly struct SimulationSettings
    {
        private static readonly double[] DefaultSpeedLevels = { 1.0, 2.0, 4.0 };

        /// <summary>Length of one tick in simulated seconds (0.02 - 5.0).</summary>
        public readonly double TickIntervalSeconds;

        /// <summary>Maximum ticks produced by a single frame (1 - 64).</summary>
        public readonly int MaxCatchUpTicksPerFrame;

        /// <summary>Maximum real delta fed into the clock by one frame (0.02 - 1.0).</summary>
        public readonly double MaxFrameDeltaSeconds;

        /// <summary>Available speed multipliers, ascending and de-duplicated.</summary>
        public readonly IReadOnlyList<double> SpeedLevels;

        /// <summary>Speed selected when a session starts; always one of <see cref="SpeedLevels"/>.</summary>
        public readonly double DefaultSpeedMultiplier;

        /// <summary>When true a new session starts in the paused state.</summary>
        public readonly bool StartPaused;

        public SimulationSettings(
            double tickIntervalSeconds,
            int maxCatchUpTicksPerFrame,
            double maxFrameDeltaSeconds,
            IReadOnlyList<double> speedLevels,
            double defaultSpeedMultiplier,
            bool startPaused)
        {
            TickIntervalSeconds = SimulationClock.ClampTickInterval(tickIntervalSeconds);
            MaxCatchUpTicksPerFrame = SimulationClock.ClampCatchUpTicks(maxCatchUpTicksPerFrame);
            MaxFrameDeltaSeconds = SimulationClock.ClampFrameDelta(maxFrameDeltaSeconds);

            double[] sanitized = SimulationSpeed.SanitizeLevels(speedLevels);
            SpeedLevels = sanitized;
            DefaultSpeedMultiplier = SimulationSpeed.NearestLevel(sanitized, defaultSpeedMultiplier);
            StartPaused = startPaused;
        }

        public int SpeedLevelCount
        {
            get { return SpeedLevels.Count; }
        }

        /// <summary>Returns a defensive copy of the speed ladder.</summary>
        public double[] CopySpeedLevels()
        {
            if (SpeedLevels == null || SpeedLevels.Count == 0)
            {
                return (double[])DefaultSpeedLevels.Clone();
            }

            double[] copy = new double[SpeedLevels.Count];
            for (int i = 0; i < copy.Length; i++)
            {
                copy[i] = SpeedLevels[i];
            }

            return copy;
        }

        public static SimulationSettings CreateDefault()
        {
            return new SimulationSettings(
                SimulationClock.DefaultTickIntervalSeconds,
                8,
                0.25,
                DefaultSpeedLevels,
                SimulationSpeed.Normal,
                false);
        }

        /// <summary>
        /// Builds settings from the project configuration. Missing or invalid values fall
        /// back to the validated defaults instead of throwing.
        /// </summary>
        public static SimulationSettings FromConfiguration(GameConfiguration configuration)
        {
            if (configuration == null)
            {
                return CreateDefault();
            }

            IReadOnlyList<float> configured = configuration.SimulationSpeedMultipliers;
            double[] levels = new double[configured != null ? configured.Count : 0];
            for (int i = 0; i < levels.Length; i++)
            {
                levels[i] = configured[i];
            }

            return new SimulationSettings(
                configuration.SimulationTickIntervalSeconds,
                configuration.MaxCatchUpTicksPerFrame,
                configuration.MaxFrameDeltaSeconds,
                levels,
                configuration.DefaultSimulationSpeedMultiplier,
                configuration.SimulationStartsPaused);
        }

        public override string ToString()
        {
            return "tick=" + TickIntervalSeconds.ToString("0.###") +
                   "s levels=" + (SpeedLevels != null ? SpeedLevels.Count : 0) +
                   " default=" + DefaultSpeedMultiplier.ToString("0.##") + "x";
        }
    }
}

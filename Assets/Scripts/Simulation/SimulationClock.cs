using System;

namespace Nexus.Simulation
{
    /// <summary>
    /// Deterministic fixed-step time source. The clock is advanced with real (frame)
    /// deltas but only ever exposes whole simulation ticks, so gameplay code never sees
    /// frame timing. It is deliberately free of Unity types and of wall-clock time:
    /// the same sequence of <see cref="Advance"/> calls always produces the same ticks.
    ///
    /// Safety rules:
    /// * frame deltas are clamped (a stall cannot fast-forward the world),
    /// * a single frame can only produce a bounded number of catch-up ticks,
    /// * backlog beyond that bound is discarded and counted, never accumulated,
    /// * while paused nothing accumulates at all.
    /// </summary>
    public sealed class SimulationClock
    {
        public const double MinimumTickIntervalSeconds = 0.02;
        public const double MaximumTickIntervalSeconds = 5.0;
        public const int MinimumCatchUpTicksPerFrame = 1;
        public const int MaximumCatchUpTicksPerFrame = 64;
        public const double MinimumFrameDeltaSeconds = 0.02;
        public const double MaximumFrameDeltaSeconds = 1.0;

        /// <summary>Default tick length: two simulation ticks per real second.</summary>
        public const double DefaultTickIntervalSeconds = 0.5;

        private const double ComparisonEpsilon = 1e-9;

        private double _tickIntervalSeconds = DefaultTickIntervalSeconds;
        private int _maxCatchUpTicksPerFrame = 8;
        private double _maxFrameDeltaSeconds = 0.25;
        private double _accumulator;
        private double _speedMultiplier = SimulationSpeed.Normal;
        private bool _paused;
        private long _tickCount;
        private double _elapsedSeconds;
        private long _droppedTicks;
        private long _clampedFrames;

        /// <summary>Length of one simulation tick in simulated seconds.</summary>
        public double TickIntervalSeconds
        {
            get { return _tickIntervalSeconds; }
        }

        /// <summary>Number of completed ticks since the last reset.</summary>
        public long TickCount
        {
            get { return _tickCount; }
        }

        /// <summary>
        /// Simulated time in seconds. Derived from the tick count and the interval so it
        /// can never drift away from the ticks the systems actually observed.
        /// </summary>
        public double ElapsedSeconds
        {
            get { return _elapsedSeconds; }
        }

        /// <summary>Current real-time multiplier applied to incoming frame deltas.</summary>
        public double SpeedMultiplier
        {
            get { return _speedMultiplier; }
        }

        /// <summary>True while the clock refuses to accumulate time.</summary>
        public bool IsPaused
        {
            get { return _paused; }
        }

        public int MaxCatchUpTicksPerFrame
        {
            get { return _maxCatchUpTicksPerFrame; }
        }

        public double MaxFrameDeltaSeconds
        {
            get { return _maxFrameDeltaSeconds; }
        }

        /// <summary>Whole ticks thrown away because a frame exceeded the catch-up budget.</summary>
        public long DroppedTicks
        {
            get { return _droppedTicks; }
        }

        /// <summary>Frames whose delta was clamped to <see cref="MaxFrameDeltaSeconds"/>.</summary>
        public long ClampedFrames
        {
            get { return _clampedFrames; }
        }

        /// <summary>
        /// Progress towards the next tick (0..1). Presentation code may use it to
        /// interpolate, but simulation systems must not.
        /// </summary>
        public double PendingTickFraction
        {
            get
            {
                if (_tickIntervalSeconds <= 0.0)
                {
                    return 0.0;
                }

                double fraction = _accumulator / _tickIntervalSeconds;
                if (fraction < 0.0)
                {
                    return 0.0;
                }

                return fraction > 1.0 ? 1.0 : fraction;
            }
        }

        /// <summary>
        /// Applies validated timing settings. Invalid values are clamped, never rejected.
        /// Configuring while ticks are pending discards the partial accumulator so the new
        /// interval starts from a known state.
        /// </summary>
        public void Configure(double tickIntervalSeconds, int maxCatchUpTicksPerFrame, double maxFrameDeltaSeconds)
        {
            _tickIntervalSeconds = ClampTickInterval(tickIntervalSeconds);
            _maxCatchUpTicksPerFrame = ClampCatchUpTicks(maxCatchUpTicksPerFrame);
            _maxFrameDeltaSeconds = ClampFrameDelta(maxFrameDeltaSeconds);
            _accumulator = 0.0;
        }

        /// <summary>Sets the real-time multiplier (clamped to the supported range).</summary>
        public void SetSpeed(double multiplier)
        {
            _speedMultiplier = SimulationSpeed.Clamp(multiplier);
        }

        /// <summary>
        /// Pauses or resumes accumulation. Pausing discards the partial tick so resuming
        /// cannot immediately fire a tick that was accumulated before the pause.
        /// </summary>
        public void SetPaused(bool paused)
        {
            if (_paused == paused)
            {
                return;
            }

            _paused = paused;
            _accumulator = 0.0;
        }

        /// <summary>Clears ticks, elapsed time, diagnostics and pause state. Speed is preserved.</summary>
        public void Reset()
        {
            _accumulator = 0.0;
            _tickCount = 0;
            _elapsedSeconds = 0.0;
            _droppedTicks = 0;
            _clampedFrames = 0;
            _paused = false;
        }

        /// <summary>
        /// Consumes a real-time delta and returns how many whole ticks completed.
        /// Never throws and never returns a negative value.
        /// </summary>
        public int Advance(double deltaSeconds)
        {
            if (_paused || double.IsNaN(deltaSeconds) || double.IsInfinity(deltaSeconds) || deltaSeconds <= 0.0)
            {
                return 0;
            }

            double frameDelta = deltaSeconds;
            if (frameDelta > _maxFrameDeltaSeconds)
            {
                frameDelta = _maxFrameDeltaSeconds;
                _clampedFrames++;
            }

            double scaled = frameDelta * _speedMultiplier;
            if (double.IsNaN(scaled) || double.IsInfinity(scaled) || scaled <= 0.0)
            {
                return 0;
            }

            _accumulator += scaled;

            int completed = 0;
            while (_accumulator + ComparisonEpsilon >= _tickIntervalSeconds && completed < _maxCatchUpTicksPerFrame)
            {
                _accumulator -= _tickIntervalSeconds;
                completed++;
                _tickCount++;
            }

            if (_accumulator < 0.0)
            {
                _accumulator = 0.0;
            }

            // Beyond the catch-up budget the remaining whole ticks are dropped instead of
            // being replayed on later frames (that would make the world run in bursts).
            if (_accumulator + ComparisonEpsilon >= _tickIntervalSeconds)
            {
                long backlog = (long)Math.Floor((_accumulator + ComparisonEpsilon) / _tickIntervalSeconds);
                if (backlog > 0)
                {
                    _droppedTicks += backlog;
                    _accumulator -= backlog * _tickIntervalSeconds;
                }

                if (_accumulator < 0.0)
                {
                    _accumulator = 0.0;
                }
            }

            _elapsedSeconds = _tickCount * _tickIntervalSeconds;
            return completed;
        }

        public static double ClampTickInterval(double value)
        {
            if (double.IsNaN(value))
            {
                return DefaultTickIntervalSeconds;
            }

            if (value < MinimumTickIntervalSeconds)
            {
                return MinimumTickIntervalSeconds;
            }

            if (value > MaximumTickIntervalSeconds)
            {
                return MaximumTickIntervalSeconds;
            }

            return value;
        }

        public static int ClampCatchUpTicks(int value)
        {
            if (value < MinimumCatchUpTicksPerFrame)
            {
                return MinimumCatchUpTicksPerFrame;
            }

            if (value > MaximumCatchUpTicksPerFrame)
            {
                return MaximumCatchUpTicksPerFrame;
            }

            return value;
        }

        public static double ClampFrameDelta(double value)
        {
            if (double.IsNaN(value))
            {
                return 0.25;
            }

            if (value < MinimumFrameDeltaSeconds)
            {
                return MinimumFrameDeltaSeconds;
            }

            if (value > MaximumFrameDeltaSeconds)
            {
                return MaximumFrameDeltaSeconds;
            }

            return value;
        }
    }
}

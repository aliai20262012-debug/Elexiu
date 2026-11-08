namespace Nexus.Simulation
{
    /// <summary>
    /// Immutable snapshot handed to every system for one tick. Systems must treat this as
    /// the only source of time: reading <c>Time.deltaTime</c> inside a system would make the
    /// simulation framerate dependent and is never required.
    /// </summary>
    public readonly struct SimulationContext
    {
        /// <summary>Monotonic tick number, starting at 1 for a fresh session.</summary>
        public readonly long Tick;

        /// <summary>Simulated seconds advanced by this tick (equal to the tick interval).</summary>
        public readonly double DeltaSeconds;

        /// <summary>Total simulated seconds after this tick completed.</summary>
        public readonly double ElapsedSeconds;

        /// <summary>Speed multiplier that was active while this tick was produced.</summary>
        public readonly double SpeedMultiplier;

        /// <summary>Identifier of the owning session; empty when no session is bound.</summary>
        public readonly string SessionId;

        public SimulationContext(long tick, double deltaSeconds, double elapsedSeconds, double speedMultiplier, string sessionId)
        {
            Tick = tick;
            DeltaSeconds = deltaSeconds;
            ElapsedSeconds = elapsedSeconds;
            SpeedMultiplier = speedMultiplier;
            SessionId = sessionId ?? string.Empty;
        }

        /// <summary>Convenience cast for legacy APIs that still accept a float delta.</summary>
        public float DeltaSecondsAsFloat
        {
            get { return (float)DeltaSeconds; }
        }

        public override string ToString()
        {
            return "tick " + Tick + " @ " + ElapsedSeconds.ToString("0.###") + "s";
        }
    }
}

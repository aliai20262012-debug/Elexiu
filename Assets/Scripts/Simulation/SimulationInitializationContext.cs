namespace Nexus.Simulation
{
    /// <summary>
    /// Immutable description of the session a system is being initialized for. Future
    /// systems (planet, economy, diplomacy) receive their deterministic seed and the
    /// session identity here, before the first tick, so they never have to reach into
    /// global state or into the scene.
    /// </summary>
    public readonly struct SimulationInitializationContext
    {
        public readonly string SessionId;
        public readonly int Seed;
        public readonly string CivilizationName;
        public readonly double TickIntervalSeconds;
        public readonly double SpeedMultiplier;

        public SimulationInitializationContext(
            string sessionId,
            int seed,
            string civilizationName,
            double tickIntervalSeconds,
            double speedMultiplier)
        {
            SessionId = sessionId ?? string.Empty;
            Seed = seed;
            CivilizationName = civilizationName ?? string.Empty;
            TickIntervalSeconds = SimulationClock.ClampTickInterval(tickIntervalSeconds);
            SpeedMultiplier = SimulationSpeed.Clamp(speedMultiplier);
        }

        /// <summary>False for the parameterless/default value, which has no session.</summary>
        public bool HasSession
        {
            get { return !string.IsNullOrEmpty(SessionId); }
        }

        /// <summary>Context used when a simulation is started without a game session.</summary>
        public static SimulationInitializationContext Empty
        {
            get
            {
                return new SimulationInitializationContext(
                    string.Empty,
                    0,
                    string.Empty,
                    SimulationClock.DefaultTickIntervalSeconds,
                    SimulationSpeed.Normal);
            }
        }

        public override string ToString()
        {
            return (HasSession ? SessionId : "<no session>") + " seed=" + Seed;
        }
    }
}

namespace Nexus.Simulation
{
    /// <summary>
    /// Lifecycle status of the simulation driver. Distinct from the application level
    /// <see cref="Nexus.Core.State.GameState"/>: the game can be in the gameplay scene while
    /// the simulation is stopped, paused or failed.
    /// </summary>
    public enum SimulationStatus
    {
        /// <summary>No session is running; the clock is reset and systems are shut down.</summary>
        Stopped = 0,

        /// <summary>Ticks are being produced and dispatched.</summary>
        Running = 1,

        /// <summary>A session is loaded but the clock refuses to accumulate time.</summary>
        Paused = 2,

        /// <summary>The last start attempt failed (system initialization error).</summary>
        Failed = 3
    }
}

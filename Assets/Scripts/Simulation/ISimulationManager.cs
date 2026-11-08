using System;

namespace Nexus.Simulation
{
    /// <summary>
    /// Deterministic fixed-step simulation driver. It owns the clock and the system graph and
    /// nothing else: no gameplay rules, no rendering, no session persistence.
    ///
    /// Frame timing enters the application in exactly one place (the gameplay scene feeding
    /// <see cref="Tick(float)"/>); everything below the driver works with whole ticks.
    /// </summary>
    public interface ISimulationManager : IDisposable
    {
        /// <summary>True while a session is loaded (running or paused).</summary>
        bool IsRunning { get; }

        /// <summary>True when the clock refuses to accumulate time.</summary>
        bool IsPaused { get; }

        /// <summary>Completed ticks since the session started.</summary>
        long TickCount { get; }

        /// <summary>Length of one tick in simulated seconds.</summary>
        float TickIntervalSeconds { get; }

        /// <summary>Simulated time elapsed in the current session.</summary>
        double ElapsedSeconds { get; }

        /// <summary>Active real-time multiplier.</summary>
        double SpeedMultiplier { get; }

        SimulationStatus Status { get; }

        /// <summary>Session the driver is currently bound to; empty when stopped.</summary>
        string SessionId { get; }

        /// <summary>Registered systems, exposed for diagnostics and extension.</summary>
        ISimulationSystemRegistry Systems { get; }

        /// <summary>Effective, validated timing settings.</summary>
        SimulationSettings Settings { get; }

        /// <summary>Legacy shorthand: replaces the timing settings with validated defaults.</summary>
        void Configure(float tickIntervalSeconds);

        /// <summary>Applies validated timing settings (discouraged while a session is active).</summary>
        void Configure(SimulationSettings settings);

        /// <summary>
        /// Initializes the system graph and starts the clock for the given session.
        /// Returns false with a reason when a session is already running or when system
        /// initialization failed (in which case the graph is rolled back).
        /// </summary>
        bool TryStartSimulation(SimulationInitializationContext context, out string error);

        /// <summary>Starts a session-less simulation; failures are logged.</summary>
        void StartSimulation();

        /// <summary>Pauses time accumulation. Ignored unless the simulation is running.</summary>
        void Pause();

        /// <summary>Resumes a paused simulation. Ignored unless it is paused.</summary>
        void Resume();

        /// <summary>Changes the speed level. Returns false for invalid or unchanged values.</summary>
        bool SetSpeed(double multiplier);

        /// <summary>Shuts systems down, stops the clock and publishes a stop event.</summary>
        void Stop();

        /// <summary>Stops the simulation and restores the configured defaults.</summary>
        void Reset();

        /// <summary>Consumes a frame delta. Only running, non-paused simulations advance.</summary>
        void Tick(float deltaTime);

        /// <summary>Double precision overload used by tests and deterministic harnesses.</summary>
        void Tick(double deltaTime);
    }
}

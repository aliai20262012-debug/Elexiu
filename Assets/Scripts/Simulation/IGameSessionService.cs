using System;

namespace Nexus.Simulation
{
    /// <summary>
    /// Owns the current play session and coordinates it with the simulation driver.
    /// Presentation code never creates sessions itself and never talks to the clock:
    /// it asks this service, which keeps session state, simulation status and events
    /// consistent with each other.
    ///
    /// Save/load integration point: a future save system serializes
    /// <see cref="GameSession"/> plus the domain snapshots, and creates a session with an
    /// explicit seed to restore it deterministically.
    /// </summary>
    public interface IGameSessionService : IDisposable
    {
        /// <summary>Current session, or null when none exists.</summary>
        GameSession Current { get; }

        /// <summary>True while a session exists in the Active or Paused state.</summary>
        bool HasActiveSession { get; }

        /// <summary>State of the current session, or <see cref="GameSessionState.None"/>.</summary>
        GameSessionState State { get; }

        /// <summary>
        /// Creates a session and starts the simulation for it. Returns false and an
        /// explanation when a session is already active, required services are missing or
        /// system initialization failed (the half-created session is terminated again).
        /// A seed of 0 requests a generated seed.
        /// </summary>
        bool TryCreateSession(string civilizationName, int seed, out GameSession session, out string error);

        /// <summary>Creates a session using the configured default name and a generated seed.</summary>
        bool TryCreateSession(out GameSession session, out string error);

        /// <summary>Pauses the active session. Returns false when it is not running.</summary>
        bool Pause();

        /// <summary>Resumes a paused session. Returns false when it is not paused.</summary>
        bool Resume();

        /// <summary>Changes the simulation speed (allowed while running or paused).</summary>
        bool SetSimulationSpeed(double multiplier);

        /// <summary>
        /// Terminates the current session: stops the simulation, publishes
        /// <see cref="Nexus.Core.Events.GameSessionEndedEvent"/> and clears the reference.
        /// Idempotent: returns false when no session exists.
        /// </summary>
        bool EndSession(SessionEndReason reason);
    }
}

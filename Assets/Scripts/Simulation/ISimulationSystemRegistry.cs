using System.Collections.Generic;

namespace Nexus.Simulation
{
    /// <summary>
    /// Deterministic system graph. Registration order never decides execution order:
    /// dependencies first, then <see cref="ISimulationSystem.ExecutionOrder"/>, then the
    /// system identifier as a stable tie-break.
    /// </summary>
    public interface ISimulationSystemRegistry
    {
        /// <summary>Number of registered systems (initialized or not).</summary>
        int Count { get; }

        /// <summary>True between a successful initialization pass and the matching shutdown.</summary>
        bool IsInitialized { get; }

        /// <summary>Resolved execution order, empty while the registry is not initialized.</summary>
        IReadOnlyList<string> ExecutionOrder { get; }

        /// <summary>
        /// Registers a system. Returns false (with a logged warning) for null systems,
        /// duplicate identifiers or registration attempts during an active session.
        /// </summary>
        bool Register(ISimulationSystem system);

        /// <summary>
        /// Orders and initializes every registered system. On any failure the systems that
        /// were already initialized are shut down again, so the registry never stays in a
        /// half-initialized state.
        /// </summary>
        SystemInitializationReport InitializeAll(SimulationInitializationContext context);

        /// <summary>
        /// Dispatches one tick in execution order and returns how many systems failed.
        /// Failures are isolated: one failing system never prevents the others from ticking.
        /// </summary>
        int TickAll(SimulationContext context);

        /// <summary>Shuts every initialized system down in reverse initialization order.</summary>
        void ShutdownAll();

        /// <summary>Shuts down and forgets all registrations (used when the application closes).</summary>
        void Clear();
    }
}

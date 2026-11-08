using System.Collections.Generic;

namespace Nexus.Simulation
{
    /// <summary>
    /// Lifecycle contract for every simulation domain system (planet, economy, civilization,
    /// technology, diplomacy, environment, AI, ...). Implementations are plain C# objects:
    /// no MonoBehaviour, no scene lookups, no direct event subscriptions. The registry owns
    /// initialization, ordering, tick dispatch and shutdown.
    ///
    /// Rules:
    /// * <see cref="Initialize"/> runs once per session, before the first tick.
    /// * <see cref="Tick"/> receives the deterministic simulation time, never frame time.
    /// * <see cref="Shutdown"/> must release everything created in Initialize and is called
    ///   in reverse initialization order.
    /// </summary>
    public interface ISimulationSystem
    {
        /// <summary>Unique, stable identifier used for ordering and diagnostics.</summary>
        string SystemId { get; }

        /// <summary>Coarse ordering hint; lower values tick first among independent systems.</summary>
        int ExecutionOrder { get; }

        /// <summary>
        /// Identifiers of systems that must be initialized (and therefore ticked) before
        /// this one. Missing dependencies are reported as initialization failures.
        /// </summary>
        IReadOnlyList<string> Dependencies { get; }

        void Initialize(SimulationInitializationContext context);

        void Tick(SimulationContext context);

        void Shutdown();
    }
}

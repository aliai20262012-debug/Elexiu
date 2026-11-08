using System;
using System.Collections.Generic;

namespace Nexus.Simulation
{
    /// <summary>
    /// Convenience base class that enforces the lifecycle order for domain systems:
    /// Tick is ignored before Initialize and after Shutdown. Extend this instead of
    /// implementing <see cref="ISimulationSystem"/> directly unless a system genuinely
    /// needs custom lifecycle semantics.
    /// </summary>
    public abstract class SimulationSystemBase : ISimulationSystem
    {
        private static readonly string[] NoDependencies = new string[0];

        protected SimulationSystemBase(string systemId, int executionOrder = 0)
        {
            if (string.IsNullOrEmpty(systemId) || systemId.Trim().Length == 0)
            {
                throw new ArgumentException("A simulation system requires a non-empty identifier.", nameof(systemId));
            }

            SystemId = systemId.Trim();
            ExecutionOrder = executionOrder;
        }

        public string SystemId { get; }

        public int ExecutionOrder { get; }

        public bool IsInitialized { get; private set; }

        public virtual IReadOnlyList<string> Dependencies
        {
            get { return NoDependencies; }
        }

        public void Initialize(SimulationInitializationContext context)
        {
            if (IsInitialized)
            {
                return;
            }

            OnInitialize(context);
            IsInitialized = true;
        }

        public void Tick(SimulationContext context)
        {
            if (!IsInitialized)
            {
                return;
            }

            OnTick(context);
        }

        public void Shutdown()
        {
            if (!IsInitialized)
            {
                return;
            }

            IsInitialized = false;
            OnShutdown();
        }

        /// <summary>Called once per session before the first tick.</summary>
        protected virtual void OnInitialize(SimulationInitializationContext context)
        {
        }

        /// <summary>Called for every simulation tick while the session is running.</summary>
        protected abstract void OnTick(SimulationContext context);

        /// <summary>Called once when the session ends; release session scoped state here.</summary>
        protected virtual void OnShutdown()
        {
        }
    }
}

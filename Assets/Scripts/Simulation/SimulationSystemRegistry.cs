using System;
using System.Collections.Generic;
using Nexus.Core.Events;
using Nexus.Core.Logging;

namespace Nexus.Simulation
{
    /// <summary>
    /// Default <see cref="ISimulationSystemRegistry"/> implementation.
    ///
    /// Ordering uses Kahn's algorithm over the declared dependencies with a stable
    /// tie-break, so the same registration set always yields the same tick order on every
    /// platform and every run. Errors are reported as data (a report) and as
    /// <see cref="SimulationSystemFailedEvent"/> messages, never as exceptions that could
    /// tear down the scene.
    /// </summary>
    public sealed class SimulationSystemRegistry : ISimulationSystemRegistry, IDisposable
    {
        /// <summary>A system that throws this many times in a row is disabled for the session.</summary>
        public const int MaxConsecutiveFailures = 3;

        private readonly List<ISimulationSystem> _registrations = new List<ISimulationSystem>();
        private readonly Dictionary<string, int> _registrationIndexById =
            new Dictionary<string, int>(StringComparer.Ordinal);

        private readonly List<string> _executionOrder = new List<string>();
        private readonly IEventBus _eventBus;

        private ISimulationSystem[] _ordered = new ISimulationSystem[0];
        private int[] _consecutiveFailures = new int[0];
        private bool[] _disabled = new bool[0];
        private bool _initialized;

        public SimulationSystemRegistry()
            : this(null)
        {
        }

        public SimulationSystemRegistry(IEventBus eventBus)
        {
            _eventBus = eventBus;
        }

        public int Count
        {
            get { return _registrations.Count; }
        }

        public bool IsInitialized
        {
            get { return _initialized; }
        }

        public IReadOnlyList<string> ExecutionOrder
        {
            get { return _executionOrder; }
        }

        public bool Register(ISimulationSystem system)
        {
            if (system == null)
            {
                GameLog.Warning("A null simulation system was ignored.");
                return false;
            }

            if (_initialized)
            {
                GameLog.Warning("Cannot register '" + system.SystemId + "' while a simulation session is active.");
                return false;
            }

            string id = system.SystemId;
            if (string.IsNullOrEmpty(id) || id.Trim().Length == 0)
            {
                GameLog.Warning("A simulation system without an identifier was ignored.");
                return false;
            }

            if (_registrationIndexById.ContainsKey(id))
            {
                GameLog.Warning("A simulation system with id '" + id + "' is already registered.");
                return false;
            }

            _registrationIndexById.Add(id, _registrations.Count);
            _registrations.Add(system);
            return true;
        }

        public SystemInitializationReport InitializeAll(SimulationInitializationContext context)
        {
            if (_initialized)
            {
                return new SystemInitializationReport(_executionOrder, null, _ordered.Length);
            }

            List<string> order;
            List<SystemInitializationFailure> failures;
            if (!TryBuildExecutionOrder(out order, out failures))
            {
                PublishFailures(failures);
                return new SystemInitializationReport(order, failures, 0);
            }

            List<ISimulationSystem> initialized = new List<ISimulationSystem>(order.Count);
            for (int i = 0; i < order.Count; i++)
            {
                ISimulationSystem system = _registrations[_registrationIndexById[order[i]]];
                try
                {
                    system.Initialize(context);
                    initialized.Add(system);
                }
                catch (Exception exception)
                {
                    SystemInitializationFailure failure =
                        new SystemInitializationFailure(system.SystemId, "Initialize", Describe(exception));
                    failures.Add(failure);
                    GameLog.Exception(exception, "Simulation system '" + system.SystemId + "' failed to initialize.");
                    PublishFailure(failure);
                    break;
                }
            }

            if (failures.Count > 0)
            {
                ShutdownInitialized(initialized);
                return new SystemInitializationReport(order, failures, 0);
            }

            _ordered = initialized.ToArray();
            _consecutiveFailures = new int[_ordered.Length];
            _disabled = new bool[_ordered.Length];

            _executionOrder.Clear();
            _executionOrder.AddRange(order);
            _initialized = true;

            return new SystemInitializationReport(order, failures, _ordered.Length);
        }

        public int TickAll(SimulationContext context)
        {
            if (!_initialized)
            {
                return 0;
            }

            int failureCount = 0;
            for (int i = 0; i < _ordered.Length; i++)
            {
                if (_disabled[i])
                {
                    continue;
                }

                try
                {
                    _ordered[i].Tick(context);
                    _consecutiveFailures[i] = 0;
                }
                catch (Exception exception)
                {
                    failureCount++;
                    _consecutiveFailures[i]++;
                    string id = _ordered[i].SystemId;
                    GameLog.Exception(exception, "Simulation system '" + id + "' failed during tick " + context.Tick + ".");

                    SystemInitializationFailure failure =
                        new SystemInitializationFailure(id, "Tick", Describe(exception));
                    PublishFailure(failure);

                    if (_consecutiveFailures[i] >= MaxConsecutiveFailures)
                    {
                        _disabled[i] = true;
                        GameLog.Error("Simulation system '" + id + "' was disabled after " + MaxConsecutiveFailures + " consecutive tick failures.");
                    }
                }
            }

            return failureCount;
        }

        public void ShutdownAll()
        {
            if (!_initialized)
            {
                return;
            }

            ShutdownInitialized(_ordered);

            _initialized = false;
            _ordered = new ISimulationSystem[0];
            _consecutiveFailures = new int[0];
            _disabled = new bool[0];
            _executionOrder.Clear();
        }

        public void Clear()
        {
            ShutdownAll();
            _registrations.Clear();
            _registrationIndexById.Clear();
            _executionOrder.Clear();
        }

        public void Dispose()
        {
            Clear();
        }

        /// <summary>True when the system was disabled because it kept throwing.</summary>
        public bool IsDisabled(string systemId)
        {
            if (!_initialized || string.IsNullOrEmpty(systemId))
            {
                return false;
            }

            for (int i = 0; i < _ordered.Length; i++)
            {
                if (string.Equals(_ordered[i].SystemId, systemId, StringComparison.Ordinal))
                {
                    return _disabled[i];
                }
            }

            return false;
        }

        private bool TryBuildExecutionOrder(out List<string> order, out List<SystemInitializationFailure> failures)
        {
            order = new List<string>(_registrations.Count);
            failures = new List<SystemInitializationFailure>();

            Dictionary<string, int> indegree = new Dictionary<string, int>(StringComparer.Ordinal);
            Dictionary<string, List<string>> dependants = new Dictionary<string, List<string>>(StringComparer.Ordinal);

            for (int i = 0; i < _registrations.Count; i++)
            {
                ISimulationSystem system = _registrations[i];
                string id = system.SystemId;
                indegree[id] = 0;
                dependants[id] = new List<string>();
            }

            for (int i = 0; i < _registrations.Count; i++)
            {
                ISimulationSystem system = _registrations[i];
                string id = system.SystemId;
                IReadOnlyList<string> dependencies = system.Dependencies;

                if (dependencies == null)
                {
                    continue;
                }

                // A dependency declared twice is an authoring slip, not a cycle: count it once.
                List<string> declared = new List<string>(dependencies.Count);

                for (int d = 0; d < dependencies.Count; d++)
                {
                    string dependency = dependencies[d];

                    if (string.IsNullOrEmpty(dependency))
                    {
                        failures.Add(new SystemInitializationFailure(id, "Ordering", "declares an empty dependency id."));
                        continue;
                    }

                    if (declared.Contains(dependency))
                    {
                        continue;
                    }

                    declared.Add(dependency);

                    if (string.Equals(dependency, id, StringComparison.Ordinal))
                    {
                        failures.Add(new SystemInitializationFailure(id, "Ordering", "cannot depend on itself."));
                        continue;
                    }

                    if (!_registrationIndexById.ContainsKey(dependency))
                    {
                        failures.Add(new SystemInitializationFailure(
                            id,
                            "Ordering",
                            "requires unregistered dependency '" + dependency + "'."));
                        continue;
                    }

                    indegree[id] = indegree[id] + 1;
                    dependants[dependency].Add(id);
                }
            }

            if (failures.Count > 0)
            {
                return false;
            }

            List<string> ready = new List<string>();
            foreach (KeyValuePair<string, int> entry in indegree)
            {
                if (entry.Value == 0)
                {
                    ready.Add(entry.Key);
                }
            }

            ready.Sort(CompareIds);

            while (ready.Count > 0)
            {
                string current = ready[0];
                ready.RemoveAt(0);
                order.Add(current);

                List<string> currentDependants = dependants[current];
                for (int i = 0; i < currentDependants.Count; i++)
                {
                    string dependant = currentDependants[i];
                    indegree[dependant] = indegree[dependant] - 1;
                    if (indegree[dependant] == 0)
                    {
                        InsertSorted(ready, dependant);
                    }
                }
            }

            if (order.Count != _registrations.Count)
            {
                List<string> unresolved = new List<string>();
                for (int i = 0; i < _registrations.Count; i++)
                {
                    if (!order.Contains(_registrations[i].SystemId))
                    {
                        unresolved.Add(_registrations[i].SystemId);
                    }
                }

                failures.Add(new SystemInitializationFailure(
                    string.Empty,
                    "Ordering",
                    "dependency cycle detected among: " + string.Join(", ", unresolved.ToArray())));
                order.Clear();
                return false;
            }

            return true;
        }

        private int CompareIds(string left, string right)
        {
            ISimulationSystem leftSystem = _registrations[_registrationIndexById[left]];
            ISimulationSystem rightSystem = _registrations[_registrationIndexById[right]];
            return CompareSystems(leftSystem, rightSystem);
        }

        private static int CompareSystems(ISimulationSystem left, ISimulationSystem right)
        {
            int byOrder = left.ExecutionOrder.CompareTo(right.ExecutionOrder);
            if (byOrder != 0)
            {
                return byOrder;
            }

            return string.CompareOrdinal(left.SystemId, right.SystemId);
        }

        private void InsertSorted(List<string> ready, string id)
        {
            int index = 0;
            while (index < ready.Count && CompareIds(ready[index], id) <= 0)
            {
                index++;
            }

            ready.Insert(index, id);
        }

        private void ShutdownInitialized(IList<ISimulationSystem> systems)
        {
            for (int i = systems.Count - 1; i >= 0; i--)
            {
                ISimulationSystem system = systems[i];
                try
                {
                    system.Shutdown();
                }
                catch (Exception exception)
                {
                    GameLog.Exception(exception, "Simulation system '" + system.SystemId + "' failed to shut down.");
                    PublishFailure(new SystemInitializationFailure(system.SystemId, "Shutdown", Describe(exception)));
                }
            }
        }

        private void PublishFailures(List<SystemInitializationFailure> failures)
        {
            if (failures == null)
            {
                return;
            }

            for (int i = 0; i < failures.Count; i++)
            {
                PublishFailure(failures[i]);
            }
        }

        private void PublishFailure(SystemInitializationFailure failure)
        {
            if (_eventBus == null)
            {
                return;
            }

            _eventBus.Publish(new SimulationSystemFailedEvent(failure.SystemId, failure.Phase, failure.Message));
        }

        private static string Describe(Exception exception)
        {
            if (exception == null)
            {
                return "unknown error";
            }

            return exception.GetType().Name + ": " + exception.Message;
        }
    }
}

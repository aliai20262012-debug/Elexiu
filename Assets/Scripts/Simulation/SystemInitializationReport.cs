using System.Collections.Generic;

namespace Nexus.Simulation
{
    /// <summary>One structured failure produced while ordering, initializing or shutting down systems.</summary>
    public readonly struct SystemInitializationFailure
    {
        /// <summary>Identifier of the failing system; empty for registry level failures.</summary>
        public readonly string SystemId;

        /// <summary>Lifecycle phase: "Ordering", "Initialize", "Tick" or "Shutdown".</summary>
        public readonly string Phase;

        public readonly string Message;

        public SystemInitializationFailure(string systemId, string phase, string message)
        {
            SystemId = systemId ?? string.Empty;
            Phase = phase ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public override string ToString()
        {
            string owner = string.IsNullOrEmpty(SystemId) ? "<registry>" : SystemId;
            return owner + " [" + Phase + "]: " + Message;
        }
    }

    /// <summary>
    /// Outcome of a system initialization pass. Returned instead of throwing so the
    /// application can report a failure, show it in the UI and retry safely.
    /// </summary>
    public sealed class SystemInitializationReport
    {
        private readonly List<string> _initializationOrder;
        private readonly List<SystemInitializationFailure> _failures;

        public SystemInitializationReport(
            IEnumerable<string> initializationOrder,
            IEnumerable<SystemInitializationFailure> failures,
            int initializedCount)
        {
            _initializationOrder = initializationOrder != null
                ? new List<string>(initializationOrder)
                : new List<string>();

            _failures = failures != null
                ? new List<SystemInitializationFailure>(failures)
                : new List<SystemInitializationFailure>();

            InitializedCount = initializedCount < 0 ? 0 : initializedCount;
        }

        public bool Succeeded
        {
            get { return _failures.Count == 0; }
        }

        public IReadOnlyList<string> InitializationOrder
        {
            get { return _initializationOrder; }
        }

        public IReadOnlyList<SystemInitializationFailure> Failures
        {
            get { return _failures; }
        }

        public int InitializedCount { get; }

        /// <summary>Single line summary suitable for logs and for the error overlay.</summary>
        public string DescribeFailures()
        {
            if (_failures.Count == 0)
            {
                return string.Empty;
            }

            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            for (int i = 0; i < _failures.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(" | ");
                }

                builder.Append(_failures[i].ToString());
            }

            return builder.ToString();
        }

        public static SystemInitializationReport EmptySuccess()
        {
            return new SystemInitializationReport(null, null, 0);
        }
    }
}

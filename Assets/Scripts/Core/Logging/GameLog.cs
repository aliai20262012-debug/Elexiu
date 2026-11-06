using UnityEngine;

namespace Nexus.Core.Logging
{
    /// <summary>
    /// Centralised, tagged logging so that console output stays greppable and
    /// every subsystem logs through one funnel that can later be routed to a
    /// diagnostics backend.
    /// </summary>
    public static class GameLog
    {
        private const string Prefix = "[NEXUS]";

        public static void Info(string message, Object context = null)
        {
            Debug.Log(Format(message), context);
        }

        public static void Warning(string message, Object context = null)
        {
            Debug.LogWarning(Format(message), context);
        }

        public static void Error(string message, Object context = null)
        {
            Debug.LogError(Format(message), context);
        }

        public static void Exception(System.Exception exception, string message, Object context = null)
        {
            Debug.LogError(Format(message) + "\n" + exception, context);
        }

        private static string Format(string message)
        {
            return Prefix + " " + (message ?? string.Empty);
        }
    }
}

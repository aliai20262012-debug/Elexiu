using System;

namespace Nexus.Simulation
{
    /// <summary>Lifecycle state of a play session.</summary>
    public enum GameSessionState
    {
        /// <summary>No session exists.</summary>
        None = 0,

        /// <summary>Session is loaded and the simulation is running.</summary>
        Active = 1,

        /// <summary>Session is loaded but the clock is paused.</summary>
        Paused = 2,

        /// <summary>Session has been terminated and can no longer be ticked.</summary>
        Ended = 3
    }

    /// <summary>Why a session ended. Used for diagnostics and for future save-on-exit logic.</summary>
    public enum SessionEndReason
    {
        PlayerRequested = 0,
        SceneUnloaded = 1,
        ApplicationQuit = 2,
        InitializationFailed = 3,
        Unknown = 4
    }

    /// <summary>
    /// Data of one play session: identity, deterministic seed, progress and lifecycle.
    /// This type contains no presentation state and no simulation logic; it is the payload
    /// future save/load code will serialize. Mutation is internal to the assembly so that
    /// only <see cref="GameSessionService"/> can change a session's state.
    /// </summary>
    public sealed class GameSession
    {
        public const int MaximumNameLength = 48;

        /// <summary>Stable identifier of the session (used by events and future save slots).</summary>
        public string SessionId { get; }

        /// <summary>Display name of the player's civilization.</summary>
        public string CivilizationName { get; }

        /// <summary>Deterministic seed for world generation and domain systems.</summary>
        public int Seed { get; }

        public DateTime CreatedAtUtc { get; }

        public GameSessionState State { get; internal set; }

        /// <summary>Last completed simulation tick.</summary>
        public long TickCount { get; internal set; }

        /// <summary>Simulated seconds observed at the last completed tick.</summary>
        public double ElapsedSeconds { get; internal set; }

        public DateTime? EndedAtUtc { get; internal set; }

        public SessionEndReason EndReason { get; internal set; }

        public GameSession(string sessionId, string civilizationName, int seed, DateTime createdAtUtc)
        {
            if (string.IsNullOrEmpty(sessionId))
            {
                throw new ArgumentException("A session requires an identifier.", nameof(sessionId));
            }

            SessionId = sessionId;
            CivilizationName = SanitizeName(civilizationName, "NEW COLONY");
            Seed = seed;
            CreatedAtUtc = createdAtUtc;
            State = GameSessionState.Active;
            EndReason = SessionEndReason.Unknown;
        }

        /// <summary>True while the session can be paused, resumed or progressed.</summary>
        public bool IsActive
        {
            get { return State == GameSessionState.Active || State == GameSessionState.Paused; }
        }

        /// <summary>Trims, bounds and defaults a player supplied civilization name.</summary>
        public static string SanitizeName(string name, string fallback)
        {
            string resolvedFallback = string.IsNullOrEmpty(fallback) ? "NEW COLONY" : fallback;

            if (string.IsNullOrEmpty(name) || name.Trim().Length == 0)
            {
                return resolvedFallback;
            }

            string trimmed = name.Trim();
            if (trimmed.Length > MaximumNameLength)
            {
                trimmed = trimmed.Substring(0, MaximumNameLength).Trim();
            }

            return trimmed.Length == 0 ? resolvedFallback : trimmed;
        }

        public override string ToString()
        {
            return CivilizationName + " (" + SessionId + ") " + State + ", tick " + TickCount;
        }
    }
}

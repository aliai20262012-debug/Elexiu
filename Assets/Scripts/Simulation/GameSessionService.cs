using System;
using Nexus.Configuration;
using Nexus.Core.Events;
using Nexus.Core.Logging;

namespace Nexus.Simulation
{
    /// <summary>
    /// Default <see cref="IGameSessionService"/>. Coordinates three things that must never
    /// drift apart: the session payload, the simulation lifecycle and the published events.
    ///
    /// Ordering guarantees:
    /// * GameSessionCreatedEvent is published before the simulation starts, so listeners
    ///   already know the session when SimulationStartedEvent arrives.
    /// * GameSessionEndedEvent is published after the simulation stopped, so listeners can
    ///   read the final tick/time values.
    /// * A failed start terminates the freshly created session again (no zombie sessions).
    /// </summary>
    public sealed class GameSessionService : IGameSessionService
    {
        private readonly IEventBus _eventBus;
        private readonly IConfigurationService _configuration;
        private readonly ISimulationManager _simulation;
        private readonly Func<string> _sessionIdFactory;
        private readonly Func<int> _seedFactory;
        private readonly Random _random = new Random();

        private GameSession _current;
        private bool _disposed;

        public GameSessionService(
            IEventBus eventBus,
            IConfigurationService configuration,
            ISimulationManager simulation)
            : this(eventBus, configuration, simulation, null, null)
        {
        }

        /// <summary>
        /// Test seam: identifier and seed factories can be replaced to make session
        /// creation fully deterministic in unit tests.
        /// </summary>
        public GameSessionService(
            IEventBus eventBus,
            IConfigurationService configuration,
            ISimulationManager simulation,
            Func<string> sessionIdFactory,
            Func<int> seedFactory)
        {
            if (eventBus == null)
            {
                throw new ArgumentNullException(nameof(eventBus));
            }

            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            if (simulation == null)
            {
                throw new ArgumentNullException(nameof(simulation));
            }

            _eventBus = eventBus;
            _configuration = configuration;
            _simulation = simulation;
            _sessionIdFactory = sessionIdFactory != null
                ? sessionIdFactory
                : CreateDefaultSessionId;
            _seedFactory = seedFactory != null
                ? seedFactory
                : CreateDefaultSeed;

            _eventBus.Subscribe<SimulationTickCompletedEvent>(HandleTickCompleted);
            _eventBus.Subscribe<SimulationStoppedEvent>(HandleSimulationStopped);
        }

        public GameSession Current
        {
            get { return _current; }
        }

        public bool HasActiveSession
        {
            get { return _current != null && _current.IsActive; }
        }

        public GameSessionState State
        {
            get { return _current != null ? _current.State : GameSessionState.None; }
        }

        public bool TryCreateSession(out GameSession session, out string error)
        {
            return TryCreateSession(null, 0, out session, out error);
        }

        public bool TryCreateSession(string civilizationName, int seed, out GameSession session, out string error)
        {
            session = null;
            error = string.Empty;

            if (_disposed)
            {
                error = "The session service has been disposed.";
                return false;
            }

            if (HasActiveSession)
            {
                error = "A session is already active. End it before creating another one.";
                return false;
            }

            string name = GameSession.SanitizeName(civilizationName, _configuration.Game.DefaultCivilizationName);
            string sessionId = _sessionIdFactory != null ? _sessionIdFactory() : string.Empty;
            if (string.IsNullOrEmpty(sessionId))
            {
                error = "Session id generation produced an empty identifier.";
                return false;
            }

            int resolvedSeed = seed != 0 ? seed : (_seedFactory != null ? _seedFactory() : 0);
            GameSession created = new GameSession(sessionId, name, resolvedSeed, DateTime.UtcNow);
            _current = created;

            _eventBus.Publish(new GameSessionCreatedEvent(created.SessionId, created.CivilizationName, created.Seed));

            SimulationInitializationContext context = new SimulationInitializationContext(
                created.SessionId,
                created.Seed,
                created.CivilizationName,
                _configuration.Game.SimulationTickIntervalSeconds,
                _configuration.Game.DefaultSimulationSpeedMultiplier);

            string startError;
            if (!_simulation.TryStartSimulation(context, out startError))
            {
                created.State = GameSessionState.Ended;
                created.EndReason = SessionEndReason.InitializationFailed;
                created.EndedAtUtc = DateTime.UtcNow;
                _current = null;

                error = string.IsNullOrEmpty(startError)
                    ? "The simulation could not be started."
                    : startError;

                _eventBus.Publish(new GameSessionEndedEvent(
                    created.SessionId,
                    SessionEndReason.InitializationFailed,
                    created.TickCount,
                    created.ElapsedSeconds));
                return false;
            }

            created.State = _simulation.Status == SimulationStatus.Paused
                ? GameSessionState.Paused
                : GameSessionState.Active;

            session = created;
            return true;
        }

        public bool Pause()
        {
            if (_current == null || _current.State != GameSessionState.Active)
            {
                return false;
            }

            if (!_simulation.IsRunning || _simulation.IsPaused)
            {
                return false;
            }

            _simulation.Pause();
            _current.State = GameSessionState.Paused;
            return true;
        }

        public bool Resume()
        {
            if (_current == null || _current.State != GameSessionState.Paused)
            {
                return false;
            }

            if (!_simulation.IsPaused)
            {
                return false;
            }

            _simulation.Resume();
            _current.State = GameSessionState.Active;
            return true;
        }

        public bool SetSimulationSpeed(double multiplier)
        {
            if (!HasActiveSession)
            {
                return false;
            }

            return _simulation.SetSpeed(multiplier);
        }

        public bool EndSession(SessionEndReason reason)
        {
            if (_current == null)
            {
                return false;
            }

            GameSession session = _current;
            _current = null;

            if (session.IsActive)
            {
                session.State = GameSessionState.Ended;
                session.EndReason = reason;
                session.EndedAtUtc = DateTime.UtcNow;
            }

            if (_simulation.IsRunning || _simulation.IsPaused)
            {
                _simulation.Stop();
            }

            _eventBus.Publish(new GameSessionEndedEvent(
                session.SessionId,
                reason,
                session.TickCount,
                session.ElapsedSeconds));
            return true;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            _eventBus.Unsubscribe<SimulationTickCompletedEvent>(HandleTickCompleted);
            _eventBus.Unsubscribe<SimulationStoppedEvent>(HandleSimulationStopped);

            if (_current != null)
            {
                EndSession(SessionEndReason.ApplicationQuit);
            }
        }

        private void HandleTickCompleted(SimulationTickCompletedEvent gameEvent)
        {
            if (_current == null || !_current.IsActive)
            {
                return;
            }

            if (!string.Equals(gameEvent.SessionId, _current.SessionId, StringComparison.Ordinal))
            {
                return;
            }

            _current.TickCount = gameEvent.Tick;
            _current.ElapsedSeconds = gameEvent.ElapsedSeconds;
        }

        /// <summary>
        /// The simulation can also be stopped directly (application shutdown, a future
        /// campaign reset). In that case the session must not stay "active" forever.
        /// </summary>
        private void HandleSimulationStopped(SimulationStoppedEvent gameEvent)
        {
            if (_current == null || !_current.IsActive)
            {
                return;
            }

            GameSession session = _current;
            session.State = GameSessionState.Ended;
            session.EndReason = SessionEndReason.Unknown;
            session.EndedAtUtc = DateTime.UtcNow;
            _current = null;

            GameLog.Warning("Simulation stopped outside the session service; session '" + session.SessionId + "' was closed.");

            _eventBus.Publish(new GameSessionEndedEvent(
                session.SessionId,
                SessionEndReason.Unknown,
                session.TickCount,
                session.ElapsedSeconds));
        }

        private string CreateDefaultSessionId()
        {
            return "session-" + Guid.NewGuid().ToString("N").Substring(0, 12);
        }

        private int CreateDefaultSeed()
        {
            int seed = _random.Next(1, int.MaxValue);
            return seed;
        }
    }
}

using Nexus.Core.State;
using Nexus.Simulation;

namespace Nexus.Core.Events
{
    public readonly struct GameStateChangedEvent : IGameEvent
    {
        public readonly GameState PreviousState;
        public readonly GameState CurrentState;

        public GameStateChangedEvent(GameState previousState, GameState currentState)
        {
            PreviousState = previousState;
            CurrentState = currentState;
        }

        public override string ToString()
        {
            return PreviousState + " -> " + CurrentState;
        }
    }

    public readonly struct SceneLoadRequestedEvent : IGameEvent
    {
        public readonly string SceneName;

        public SceneLoadRequestedEvent(string sceneName)
        {
            SceneName = sceneName;
        }
    }

    public readonly struct SceneLoadCompletedEvent : IGameEvent
    {
        public readonly string SceneName;
        public readonly bool Succeeded;
        public readonly string Error;

        public SceneLoadCompletedEvent(string sceneName, bool succeeded, string error)
        {
            SceneName = sceneName;
            Succeeded = succeeded;
            Error = error;
        }
    }

    public readonly struct ApplicationInitializedEvent : IGameEvent
    {
        public readonly string Version;

        public ApplicationInitializedEvent(string version)
        {
            Version = version;
        }
    }

    public readonly struct ApplicationInitializationFailedEvent : IGameEvent
    {
        public readonly string Reason;

        public ApplicationInitializationFailedEvent(string reason)
        {
            Reason = reason;
        }
    }

    public readonly struct NewGameRequestedEvent : IGameEvent
    {
    }

    public readonly struct ReturnToMenuRequestedEvent : IGameEvent
    {
    }

    public readonly struct SimulationTickEvent : IGameEvent
    {
        public readonly long Tick;
        public readonly float DeltaSeconds;

        public SimulationTickEvent(long tick, float deltaSeconds)
        {
            Tick = tick;
            DeltaSeconds = deltaSeconds;
        }
    }

    public readonly struct SettingsChangedEvent : IGameEvent
    {
        public readonly int QualityLevel;
        public readonly float MasterVolume;

        public SettingsChangedEvent(int qualityLevel, float masterVolume)
        {
            QualityLevel = qualityLevel;
            MasterVolume = masterVolume;
        }
    }

    // ---------------------------------------------------------------------------------
    // Phase 02 - simulation lifecycle and game session events.
    //
    // Two tick events exist on purpose and are not duplicates:
    // * SimulationTickEvent is the per-system tick notification (domain signal).
    // * SimulationTickCompletedEvent is published after every system ran, carrying the
    //   tick outcome (elapsed time, failures) and is what presentation code listens to.
    // ---------------------------------------------------------------------------------

    public readonly struct SimulationStartedEvent : IGameEvent
    {
        public readonly string SessionId;
        public readonly long Tick;
        public readonly double SpeedMultiplier;

        public SimulationStartedEvent(string sessionId, long tick, double speedMultiplier)
        {
            SessionId = sessionId ?? string.Empty;
            Tick = tick;
            SpeedMultiplier = speedMultiplier;
        }
    }

    public readonly struct SimulationStoppedEvent : IGameEvent
    {
        public readonly string SessionId;
        public readonly long TickCount;
        public readonly double ElapsedSeconds;
        public readonly string Reason;

        public SimulationStoppedEvent(string sessionId, long tickCount, double elapsedSeconds, string reason)
        {
            SessionId = sessionId ?? string.Empty;
            TickCount = tickCount;
            ElapsedSeconds = elapsedSeconds;
            Reason = reason ?? string.Empty;
        }
    }

    public readonly struct SimulationPausedEvent : IGameEvent
    {
        public readonly long Tick;
        public readonly double ElapsedSeconds;

        public SimulationPausedEvent(long tick, double elapsedSeconds)
        {
            Tick = tick;
            ElapsedSeconds = elapsedSeconds;
        }
    }

    public readonly struct SimulationResumedEvent : IGameEvent
    {
        public readonly long Tick;
        public readonly double ElapsedSeconds;

        public SimulationResumedEvent(long tick, double elapsedSeconds)
        {
            Tick = tick;
            ElapsedSeconds = elapsedSeconds;
        }
    }

    public readonly struct SimulationSpeedChangedEvent : IGameEvent
    {
        public readonly double PreviousSpeed;
        public readonly double NewSpeed;

        public SimulationSpeedChangedEvent(double previousSpeed, double newSpeed)
        {
            PreviousSpeed = previousSpeed;
            NewSpeed = newSpeed;
        }
    }

    public readonly struct SimulationTickCompletedEvent : IGameEvent
    {
        public readonly string SessionId;
        public readonly long Tick;
        public readonly double DeltaSeconds;
        public readonly double ElapsedSeconds;
        public readonly double SpeedMultiplier;
        public readonly int SystemsTicked;
        public readonly int SystemsFailed;

        public SimulationTickCompletedEvent(
            string sessionId,
            long tick,
            double deltaSeconds,
            double elapsedSeconds,
            double speedMultiplier,
            int systemsTicked,
            int systemsFailed)
        {
            SessionId = sessionId ?? string.Empty;
            Tick = tick;
            DeltaSeconds = deltaSeconds;
            ElapsedSeconds = elapsedSeconds;
            SpeedMultiplier = speedMultiplier;
            SystemsTicked = systemsTicked;
            SystemsFailed = systemsFailed;
        }

        public bool HadFailures
        {
            get { return SystemsFailed > 0; }
        }
    }

    public readonly struct SimulationSystemFailedEvent : IGameEvent
    {
        public readonly string SystemId;
        public readonly string Phase;
        public readonly string Message;

        public SimulationSystemFailedEvent(string systemId, string phase, string message)
        {
            SystemId = systemId ?? string.Empty;
            Phase = phase ?? string.Empty;
            Message = message ?? string.Empty;
        }
    }

    public readonly struct GameSessionCreatedEvent : IGameEvent
    {
        public readonly string SessionId;
        public readonly string CivilizationName;
        public readonly int Seed;

        public GameSessionCreatedEvent(string sessionId, string civilizationName, int seed)
        {
            SessionId = sessionId ?? string.Empty;
            CivilizationName = civilizationName ?? string.Empty;
            Seed = seed;
        }
    }

    public readonly struct GameSessionEndedEvent : IGameEvent
    {
        public readonly string SessionId;
        public readonly SessionEndReason Reason;
        public readonly long TickCount;
        public readonly double ElapsedSeconds;

        public GameSessionEndedEvent(string sessionId, SessionEndReason reason, long tickCount, double elapsedSeconds)
        {
            SessionId = sessionId ?? string.Empty;
            Reason = reason;
            TickCount = tickCount;
            ElapsedSeconds = elapsedSeconds;
        }
    }
}

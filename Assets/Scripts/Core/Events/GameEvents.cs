using Nexus.Core.State;

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
}

namespace Nexus.Simulation
{
    /// <summary>
    /// Fixed step simulation driver. Phase 01 only maintains the clock and broadcasts ticks;
    /// planet, civilization, economy and AI systems subscribe in later phases.
    /// </summary>
    public interface ISimulationManager
    {
        bool IsRunning { get; }

        bool IsPaused { get; }

        long TickCount { get; }

        float TickIntervalSeconds { get; }

        void Configure(float tickIntervalSeconds);

        void StartSimulation();

        void Pause();

        void Resume();

        void Stop();

        void Tick(float deltaTime);
    }
}

using Nexus.Core.Events;
using Nexus.Core.Logging;
using UnityEngine;

namespace Nexus.Simulation
{
    /// <summary>
    /// Accumulator based fixed step clock. Deliberately contains no gameplay logic:
    /// domain systems will be registered against <see cref="SimulationTickEvent"/>.
    /// </summary>
    public sealed class SimulationManager : ISimulationManager
    {
        private const float MinimumTickInterval = 0.02f;

        private readonly IEventBus _eventBus;
        private float _tickIntervalSeconds = 0.5f;
        private float _accumulator;

        public bool IsRunning { get; private set; }

        public bool IsPaused { get; private set; }

        public long TickCount { get; private set; }

        public float TickIntervalSeconds
        {
            get { return _tickIntervalSeconds; }
        }

        public SimulationManager(IEventBus eventBus)
        {
            if (eventBus == null)
            {
                throw new System.ArgumentNullException(nameof(eventBus));
            }

            _eventBus = eventBus;
        }

        public void Configure(float tickIntervalSeconds)
        {
            _tickIntervalSeconds = Mathf.Max(MinimumTickInterval, tickIntervalSeconds);
            _accumulator = 0f;
        }

        public void StartSimulation()
        {
            IsRunning = true;
            IsPaused = false;
            _accumulator = 0f;
            GameLog.Info("Simulation started (fixed step " + _tickIntervalSeconds.ToString("0.###") + "s).");
        }

        public void Pause()
        {
            IsPaused = true;
        }

        public void Resume()
        {
            if (IsRunning)
            {
                IsPaused = false;
            }
        }

        public void Stop()
        {
            IsRunning = false;
            IsPaused = false;
            GameLog.Info("Simulation stopped after " + TickCount + " ticks.");
        }

        public void Tick(float deltaTime)
        {
            if (!IsRunning || IsPaused || deltaTime <= 0f)
            {
                return;
            }

            _accumulator += deltaTime;

            // Bounded loop: a long frame hitch must never freeze the application.
            int safetyCounter = 0;
            while (_accumulator >= _tickIntervalSeconds && safetyCounter < 8)
            {
                _accumulator -= _tickIntervalSeconds;
                TickCount++;
                safetyCounter++;
                _eventBus.Publish(new SimulationTickEvent(TickCount, _tickIntervalSeconds));
            }

            if (safetyCounter >= 8)
            {
                _accumulator = 0f;
            }
        }
    }
}

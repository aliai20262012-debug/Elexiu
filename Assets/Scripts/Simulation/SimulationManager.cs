using System;
using Nexus.Core.Events;
using Nexus.Core.Logging;

namespace Nexus.Simulation
{
    /// <summary>
    /// Default simulation driver: fixed-step clock + ordered system graph + lifecycle events.
    ///
    /// A frame delta is converted into zero or more whole ticks. Each tick publishes the
    /// domain tick event, dispatches every registered system with an immutable
    /// <see cref="SimulationContext"/> and finally publishes the completion event used by
    /// presentation code. Nothing in this class reads Unity frame time, so the same input
    /// sequence always produces the same ticks.
    /// </summary>
    public sealed class SimulationManager : ISimulationManager
    {
        private readonly IEventBus _eventBus;
        private readonly SimulationClock _clock = new SimulationClock();
        private readonly ISimulationSystemRegistry _systems;
        private readonly bool _ownsSystemRegistry;

        private SimulationSettings _settings = SimulationSettings.CreateDefault();
        private SimulationStatus _status = SimulationStatus.Stopped;
        private string _sessionId = string.Empty;
        private bool _disposed;

        public SimulationManager(IEventBus eventBus)
            : this(eventBus, null)
        {
        }

        /// <summary>
        /// Allows injecting a pre-populated registry (composition root, tests). When no
        /// registry is supplied the manager creates and owns one.
        /// </summary>
        public SimulationManager(IEventBus eventBus, ISimulationSystemRegistry systems)
        {
            if (eventBus == null)
            {
                throw new ArgumentNullException(nameof(eventBus));
            }

            _eventBus = eventBus;
            _ownsSystemRegistry = systems == null;
            _systems = systems ?? new SimulationSystemRegistry(eventBus);

            ApplySettings();
        }

        public bool IsRunning
        {
            get { return _status == SimulationStatus.Running || _status == SimulationStatus.Paused; }
        }

        public bool IsPaused
        {
            get { return _status == SimulationStatus.Paused; }
        }

        public long TickCount
        {
            get { return _clock.TickCount; }
        }

        public float TickIntervalSeconds
        {
            get { return (float)_clock.TickIntervalSeconds; }
        }

        public double ElapsedSeconds
        {
            get { return _clock.ElapsedSeconds; }
        }

        public double SpeedMultiplier
        {
            get { return _clock.SpeedMultiplier; }
        }

        public SimulationStatus Status
        {
            get { return _status; }
        }

        public string SessionId
        {
            get { return _sessionId; }
        }

        public ISimulationSystemRegistry Systems
        {
            get { return _systems; }
        }

        public SimulationSettings Settings
        {
            get { return _settings; }
        }

        public void Configure(float tickIntervalSeconds)
        {
            SimulationSettings defaults = SimulationSettings.CreateDefault();
            Configure(new SimulationSettings(
                tickIntervalSeconds,
                defaults.MaxCatchUpTicksPerFrame,
                defaults.MaxFrameDeltaSeconds,
                defaults.SpeedLevels,
                defaults.DefaultSpeedMultiplier,
                defaults.StartPaused));
        }

        public void Configure(SimulationSettings settings)
        {
            if (IsRunning)
            {
                GameLog.Warning("Simulation timing was reconfigured while a session is active; the clock was rebased.");
            }

            _settings = settings;
            ApplySettings();
        }

        public bool TryStartSimulation(SimulationInitializationContext context, out string error)
        {
            error = string.Empty;

            if (_disposed)
            {
                error = "The simulation manager has been disposed.";
                return false;
            }

            if (IsRunning)
            {
                error = "A simulation session is already running.";
                return false;
            }

            _sessionId = context.SessionId ?? string.Empty;
            _clock.Reset();
            _clock.Configure(_settings.TickIntervalSeconds, _settings.MaxCatchUpTicksPerFrame, _settings.MaxFrameDeltaSeconds);

            double requestedSpeed = context.SpeedMultiplier > 0.0
                ? context.SpeedMultiplier
                : _settings.DefaultSpeedMultiplier;
            _clock.SetSpeed(requestedSpeed);

            SimulationInitializationContext initialization = new SimulationInitializationContext(
                _sessionId,
                context.Seed,
                context.CivilizationName,
                _clock.TickIntervalSeconds,
                _clock.SpeedMultiplier);

            SystemInitializationReport report = _systems.InitializeAll(initialization);
            if (!report.Succeeded)
            {
                error = report.DescribeFailures();
                _status = SimulationStatus.Failed;
                _sessionId = string.Empty;
                _clock.Reset();
                GameLog.Error("Simulation could not start: " + error);
                return false;
            }

            bool startPaused = _settings.StartPaused;
            _clock.SetPaused(startPaused);
            _status = startPaused ? SimulationStatus.Paused : SimulationStatus.Running;

            _eventBus.Publish(new SimulationStartedEvent(_sessionId, _clock.TickCount, _clock.SpeedMultiplier));
            if (startPaused)
            {
                _eventBus.Publish(new SimulationPausedEvent(_clock.TickCount, _clock.ElapsedSeconds));
            }

            GameLog.Info(
                "Simulation started (" + initialization + ", fixed step " +
                _clock.TickIntervalSeconds.ToString("0.###") + "s, " + _systems.Count + " system(s)).");
            return true;
        }

        public void StartSimulation()
        {
            string error;
            if (!TryStartSimulation(SimulationInitializationContext.Empty, out error))
            {
                GameLog.Warning("Simulation start rejected: " + error);
            }
        }

        public void Pause()
        {
            if (_status != SimulationStatus.Running)
            {
                GameLog.Warning("Pause ignored: the simulation is " + _status + ".");
                return;
            }

            _clock.SetPaused(true);
            _status = SimulationStatus.Paused;
            _eventBus.Publish(new SimulationPausedEvent(_clock.TickCount, _clock.ElapsedSeconds));
        }

        public void Resume()
        {
            if (_status != SimulationStatus.Paused)
            {
                GameLog.Warning("Resume ignored: the simulation is " + _status + ".");
                return;
            }

            _clock.SetPaused(false);
            _status = SimulationStatus.Running;
            _eventBus.Publish(new SimulationResumedEvent(_clock.TickCount, _clock.ElapsedSeconds));
        }

        public bool SetSpeed(double multiplier)
        {
            if (!SimulationSpeed.IsValid(multiplier))
            {
                GameLog.Warning("Simulation speed " + multiplier + " is outside the supported range; ignored.");
                return false;
            }

            double next = SimulationSpeed.Clamp(multiplier);
            double previous = _clock.SpeedMultiplier;
            if (Math.Abs(next - previous) < 0.0001)
            {
                return false;
            }

            _clock.SetSpeed(next);
            _eventBus.Publish(new SimulationSpeedChangedEvent(previous, next));
            return true;
        }

        public void Stop()
        {
            bool wasActive = _status == SimulationStatus.Running || _status == SimulationStatus.Paused;

            _systems.ShutdownAll();

            string sessionId = _sessionId;
            long tickCount = _clock.TickCount;
            double elapsedSeconds = _clock.ElapsedSeconds;

            _status = SimulationStatus.Stopped;
            _sessionId = string.Empty;
            _clock.Reset();

            if (wasActive)
            {
                _eventBus.Publish(new SimulationStoppedEvent(sessionId, tickCount, elapsedSeconds, "STOPPED"));
                GameLog.Info("Simulation stopped after " + tickCount + " ticks (" + elapsedSeconds.ToString("0.###") + "s simulated).");
            }
        }

        public void Reset()
        {
            Stop();
            ApplySettings();
            _clock.SetSpeed(_settings.DefaultSpeedMultiplier);
            _status = SimulationStatus.Stopped;
        }

        public void Tick(float deltaTime)
        {
            Tick((double)deltaTime);
        }

        public void Tick(double deltaTime)
        {
            if (_status != SimulationStatus.Running)
            {
                return;
            }

            int completedTicks = _clock.Advance(deltaTime);
            if (completedTicks <= 0)
            {
                return;
            }

            long firstTick = _clock.TickCount - completedTicks + 1;
            double interval = _clock.TickIntervalSeconds;
            double speed = _clock.SpeedMultiplier;
            int systemsTicked = _systems.Count;

            for (int i = 0; i < completedTicks; i++)
            {
                long tick = firstTick + i;
                double elapsed = tick * interval;
                SimulationContext context = new SimulationContext(tick, interval, elapsed, speed, _sessionId);

                // Domain signal: systems and future gameplay listeners react to the tick.
                _eventBus.Publish(new SimulationTickEvent(tick, (float)interval));

                int failedSystems = _systems.TickAll(context);

                // Outcome signal: presentation/telemetry consumers read the completed state.
                _eventBus.Publish(new SimulationTickCompletedEvent(
                    _sessionId,
                    tick,
                    interval,
                    elapsed,
                    speed,
                    systemsTicked,
                    failedSystems));
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Stop();

            if (_ownsSystemRegistry && _systems is IDisposable)
            {
                ((IDisposable)_systems).Dispose();
            }

            _disposed = true;
        }

        private void ApplySettings()
        {
            _clock.Configure(_settings.TickIntervalSeconds, _settings.MaxCatchUpTicksPerFrame, _settings.MaxFrameDeltaSeconds);

            if (_status == SimulationStatus.Stopped)
            {
                _clock.SetSpeed(_settings.DefaultSpeedMultiplier);
            }
            else
            {
                _clock.SetSpeed(SimulationSpeed.NearestLevel(_settings.SpeedLevels, _clock.SpeedMultiplier));
            }
        }
    }
}

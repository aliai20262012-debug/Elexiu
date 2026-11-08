using System.Collections.Generic;
using NUnit.Framework;
using Nexus.Core.Events;
using Nexus.Simulation;

namespace Nexus.Tests.EditMode
{
    /// <summary>
    /// Lifecycle tests for the simulation driver: start/stop, pause/resume, speed control,
    /// tick production and event delivery. Everything is driven with explicit deltas, so the
    /// tests prove that the driver does not depend on frame rate.
    /// </summary>
    public class SimulationManagerTests
    {
        private sealed class RecordingSystem : SimulationSystemBase
        {
            public RecordingSystem(string systemId, int executionOrder = 0)
                : base(systemId, executionOrder)
            {
            }

            public int TickCount { get; private set; }

            public long LastTick { get; private set; }

            public int InitializeCount { get; private set; }

            public int ShutdownCount { get; private set; }

            public bool ThrowOnInitialize { get; set; }

            protected override void OnInitialize(SimulationInitializationContext context)
            {
                if (ThrowOnInitialize)
                {
                    throw new System.InvalidOperationException("boom-init");
                }

                InitializeCount++;
            }

            protected override void OnTick(SimulationContext context)
            {
                TickCount++;
                LastTick = context.Tick;
            }

            protected override void OnShutdown()
            {
                ShutdownCount++;
            }
        }

        private const double Interval = 0.25;

        private EventBus _eventBus;
        private SimulationManager _manager;

        [SetUp]
        public void SetUp()
        {
            _eventBus = new EventBus();
            _manager = new SimulationManager(_eventBus);
            _manager.Configure(CreateSettings());
        }

        [TearDown]
        public void TearDown()
        {
            _manager.Dispose();
        }

        private static SimulationSettings CreateSettings(bool startPaused = false)
        {
            return new SimulationSettings(
                Interval,
                8,
                Interval,
                new[] { 1.0, 2.0, 4.0 },
                1.0,
                startPaused);
        }

        private static SimulationInitializationContext CreateSessionContext(string sessionId = "session-test")
        {
            return new SimulationInitializationContext(sessionId, 1234, "TEST CIVILIZATION", Interval, 1.0);
        }

        private void Start(string sessionId = "session-test")
        {
            string error;
            bool started = _manager.TryStartSimulation(CreateSessionContext(sessionId), out error);
            Assert.IsTrue(started, "Simulation should start: " + error);
        }

        [Test]
        public void NewManager_IsStopped()
        {
            Assert.AreEqual(SimulationStatus.Stopped, _manager.Status);
            Assert.IsFalse(_manager.IsRunning);
            Assert.IsFalse(_manager.IsPaused);
            Assert.AreEqual(0L, _manager.TickCount);
            Assert.AreEqual(string.Empty, _manager.SessionId);
            Assert.AreEqual(Interval, _manager.TickIntervalSeconds, 0.0001);
        }

        [Test]
        public void StartSimulation_InitializesSystems_AndBindsSession()
        {
            RecordingSystem system = new RecordingSystem("alpha");
            _manager.Systems.Register(system);

            Start("session-42");

            Assert.AreEqual(SimulationStatus.Running, _manager.Status);
            Assert.AreEqual("session-42", _manager.SessionId);
            Assert.AreEqual(1, system.InitializeCount);
            Assert.AreEqual(1, _manager.Systems.Count);
        }

        [Test]
        public void StartSimulation_PublishesStartedEvent()
        {
            SimulationStartedEvent captured = default(SimulationStartedEvent);
            bool received = false;
            _eventBus.Subscribe<SimulationStartedEvent>(gameEvent =>
            {
                captured = gameEvent;
                received = true;
            });

            Start("session-7");

            Assert.IsTrue(received);
            Assert.AreEqual("session-7", captured.SessionId);
            Assert.AreEqual(0L, captured.Tick);
            Assert.AreEqual(1.0, captured.SpeedMultiplier, 0.0001);
        }

        [Test]
        public void Tick_ProducesWholeTicksOnly()
        {
            Start();

            _manager.Tick(0.1);
            Assert.AreEqual(0L, _manager.TickCount);

            _manager.Tick(0.15);
            Assert.AreEqual(1L, _manager.TickCount);
            Assert.AreEqual(Interval, _manager.ElapsedSeconds, 0.0001);

            _manager.Tick(1.0);
            Assert.AreEqual(2L, _manager.TickCount, "A frame longer than the interval must be clamped to one interval.");
        }

        [Test]
        public void Tick_PublishesTickAndCompletedEvents()
        {
            List<long> ticks = new List<long>();
            List<SimulationTickCompletedEvent> completed = new List<SimulationTickCompletedEvent>();

            _eventBus.Subscribe<SimulationTickEvent>(gameEvent => ticks.Add(gameEvent.Tick));
            _eventBus.Subscribe<SimulationTickCompletedEvent>(gameEvent => completed.Add(gameEvent));

            Start("session-events");
            _manager.Tick(0.25);
            _manager.Tick(0.25);

            CollectionAssert.AreEqual(new long[] { 1L, 2L }, ticks);
            Assert.AreEqual(2, completed.Count);
            Assert.AreEqual(1L, completed[0].Tick);
            Assert.AreEqual(0.25, completed[0].ElapsedSeconds, 0.0001);
            Assert.AreEqual(0.5, completed[1].ElapsedSeconds, 0.0001);
            Assert.AreEqual("session-events", completed[1].SessionId);
            Assert.AreEqual(0, completed[1].SystemsFailed);
            Assert.IsFalse(completed[1].HadFailures);
        }

        [Test]
        public void Tick_DispatchesSystemsWithDeterministicContext()
        {
            RecordingSystem system = new RecordingSystem("alpha");
            _manager.Systems.Register(system);
            Start();

            _manager.Tick(0.25);
            _manager.Tick(0.25);

            Assert.AreEqual(2, system.TickCount);
            Assert.AreEqual(2L, system.LastTick);
        }

        [Test]
        public void Tick_DoesNothingWhenNotRunning()
        {
            _manager.Tick(10.0);

            Assert.AreEqual(0L, _manager.TickCount);
        }

        [Test]
        public void Pause_StopsTicks_AndPublishesEvent()
        {
            Start();
            _manager.Tick(Interval);
            Assert.AreEqual(1L, _manager.TickCount);

            SimulationPausedEvent captured = default(SimulationPausedEvent);
            bool received = false;
            _eventBus.Subscribe<SimulationPausedEvent>(gameEvent =>
            {
                captured = gameEvent;
                received = true;
            });

            _manager.Pause();

            Assert.IsTrue(_manager.IsPaused);
            Assert.AreEqual(SimulationStatus.Paused, _manager.Status);
            Assert.IsTrue(_manager.IsRunning, "A paused session is still loaded.");
            Assert.IsTrue(received);
            Assert.AreEqual(1L, captured.Tick);

            _manager.Tick(10.0);
            Assert.AreEqual(1L, _manager.TickCount, "Paused simulations must not advance.");
        }

        [Test]
        public void Resume_RestartsTicks_AndPublishesEvent()
        {
            Start();
            _manager.Pause();

            bool received = false;
            _eventBus.Subscribe<SimulationResumedEvent>(gameEvent => received = true);

            _manager.Resume();
            _manager.Tick(Interval);

            Assert.IsTrue(received);
            Assert.AreEqual(SimulationStatus.Running, _manager.Status);
            Assert.AreEqual(1L, _manager.TickCount);
        }

        [Test]
        public void PauseAndResume_AreIgnoredInWrongState()
        {
            Assert.DoesNotThrow(_manager.Pause);
            Assert.AreEqual(SimulationStatus.Stopped, _manager.Status);

            Start();
            Assert.DoesNotThrow(_manager.Resume);
            Assert.AreEqual(SimulationStatus.Running, _manager.Status);
        }

        [Test]
        public void SetSpeed_ChangesRate_AndPublishesEvent()
        {
            Start();

            SimulationSpeedChangedEvent captured = default(SimulationSpeedChangedEvent);
            _eventBus.Subscribe<SimulationSpeedChangedEvent>(gameEvent => captured = gameEvent);

            Assert.IsTrue(_manager.SetSpeed(4.0));
            Assert.AreEqual(4.0, _manager.SpeedMultiplier, 0.0001);
            Assert.AreEqual(1.0, captured.PreviousSpeed, 0.0001);
            Assert.AreEqual(4.0, captured.NewSpeed, 0.0001);

            _manager.Tick(Interval);
            Assert.AreEqual(4L, _manager.TickCount, "At 4x a single interval produces four ticks.");
        }

        [Test]
        public void SetSpeed_RejectsInvalidAndUnchangedValues()
        {
            Start();

            Assert.IsFalse(_manager.SetSpeed(double.NaN));
            Assert.IsFalse(_manager.SetSpeed(0.0));
            Assert.IsFalse(_manager.SetSpeed(-2.0));
            Assert.IsFalse(_manager.SetSpeed(1000.0));
            Assert.IsFalse(_manager.SetSpeed(1.0), "Setting the current speed again must not raise an event.");
        }

        [Test]
        public void Stop_ShutsSystemsDown_AndResetsClock()
        {
            RecordingSystem system = new RecordingSystem("alpha");
            _manager.Systems.Register(system);
            Start();
            _manager.Tick(Interval);

            _manager.Stop();

            Assert.AreEqual(SimulationStatus.Stopped, _manager.Status);
            Assert.AreEqual(0L, _manager.TickCount);
            Assert.AreEqual(0.0, _manager.ElapsedSeconds, 0.0001);
            Assert.AreEqual(string.Empty, _manager.SessionId);
            Assert.AreEqual(1, system.ShutdownCount);
            Assert.IsFalse(_manager.Systems.IsInitialized);
        }

        [Test]
        public void Stop_PublishesFinalValues()
        {
            Start("session-stop");
            _manager.Tick(0.25);
            _manager.Tick(0.25);

            SimulationStoppedEvent captured = default(SimulationStoppedEvent);
            bool received = false;
            _eventBus.Subscribe<SimulationStoppedEvent>(gameEvent =>
            {
                captured = gameEvent;
                received = true;
            });

            _manager.Stop();

            Assert.IsTrue(received);
            Assert.AreEqual("session-stop", captured.SessionId);
            Assert.AreEqual(2L, captured.TickCount);
            Assert.AreEqual(0.5, captured.ElapsedSeconds, 0.0001);
        }

        [Test]
        public void RestartAfterStop_StartsFromACleanClock()
        {
            RecordingSystem system = new RecordingSystem("alpha");
            _manager.Systems.Register(system);

            Start();
            _manager.Tick(0.5);
            _manager.Stop();

            Start();

            Assert.AreEqual(0L, _manager.TickCount);
            Assert.AreEqual(2, system.InitializeCount);
            Assert.AreEqual(1, system.ShutdownCount);

            _manager.Tick(Interval);
            Assert.AreEqual(1L, _manager.TickCount);
        }

        [Test]
        public void StartTwice_IsRejected()
        {
            Start();

            string error;
            bool started = _manager.TryStartSimulation(CreateSessionContext("other"), out error);

            Assert.IsFalse(started);
            Assert.IsFalse(string.IsNullOrEmpty(error));
            Assert.AreEqual("session-test", _manager.SessionId);
            Assert.AreEqual(SimulationStatus.Running, _manager.Status);
        }

        [Test]
        public void StartWithFailingSystem_ReportsFailure_AndRollsBack()
        {
            RecordingSystem failing = new RecordingSystem("alpha") { ThrowOnInitialize = true };
            _manager.Systems.Register(failing);

            string error;
            bool started = _manager.TryStartSimulation(CreateSessionContext(), out error);

            Assert.IsFalse(started);
            Assert.IsFalse(string.IsNullOrEmpty(error));
            StringAssert.Contains("alpha", error);
            Assert.AreEqual(SimulationStatus.Failed, _manager.Status);
            Assert.AreEqual(string.Empty, _manager.SessionId);
            Assert.IsFalse(_manager.Systems.IsInitialized);
        }

        [Test]
        public void Tick_ClampsHugeDelta_ToCatchUpBudget()
        {
            _manager.Configure(new SimulationSettings(0.1, 4, 1.0, new[] { 1.0 }, 1.0, false));
            Start();

            _manager.Tick(1000.0);

            Assert.AreEqual(4L, _manager.TickCount, "The catch-up budget bounds work per frame.");
        }

        [Test]
        public void StartPausedSetting_StartsInPausedState()
        {
            _manager.Configure(CreateSettings(true));
            Start();

            Assert.AreEqual(SimulationStatus.Paused, _manager.Status);
            Assert.IsTrue(_manager.IsPaused);

            _manager.Tick(Interval);
            Assert.AreEqual(0L, _manager.TickCount);

            _manager.Resume();
            _manager.Tick(Interval);
            Assert.AreEqual(1L, _manager.TickCount);
        }

        [Test]
        public void Configure_AppliesValidatedSettings()
        {
            _manager.Configure(new SimulationSettings(-10.0, 0, 1000.0, null, double.NaN, false));

            Assert.AreEqual(SimulationClock.MinimumTickIntervalSeconds, _manager.Settings.TickIntervalSeconds, 0.0001);
            Assert.AreEqual(SimulationClock.MinimumCatchUpTicksPerFrame, _manager.Settings.MaxCatchUpTicksPerFrame);
            Assert.AreEqual(SimulationClock.MaximumFrameDeltaSeconds, _manager.Settings.MaxFrameDeltaSeconds, 0.0001);
            Assert.AreEqual(3, _manager.Settings.SpeedLevelCount);
            Assert.AreEqual(Interval, _manager.TickIntervalSeconds, 0.0001);

            _manager.Configure(0.5f);
            Assert.AreEqual(0.5, _manager.TickIntervalSeconds, 0.0001);
        }

        [Test]
        public void PauseDuringTickEvent_StopsRemainingTicks()
        {
            ConfigureFastClock();
            Start();

            int tickEvents = 0;
            int completedEvents = 0;
            _eventBus.Subscribe<SimulationTickEvent>(gameEvent =>
            {
                tickEvents++;
                _manager.Pause();
            });
            _eventBus.Subscribe<SimulationTickCompletedEvent>(gameEvent => completedEvents++);

            _manager.Tick(0.5);

            Assert.AreEqual(1, tickEvents, "Only the tick already being dispatched may be published.");
            Assert.AreEqual(0, completedEvents, "A paused tick must not be reported as completed.");
            Assert.AreEqual(SimulationStatus.Paused, _manager.Status);
            Assert.AreEqual(1L, _manager.TickCount, "Undispatched ticks must be rolled back.");
            Assert.AreEqual(0.1, _manager.ElapsedSeconds, 0.0001);
        }

        [Test]
        public void StopDuringTickEvent_PublishesNoStaleTicks()
        {
            ConfigureFastClock();
            Start("session-stop");

            int tickEvents = 0;
            int completedEvents = 0;
            int stoppedEvents = 0;
            _eventBus.Subscribe<SimulationTickEvent>(gameEvent =>
            {
                tickEvents++;
                _manager.Stop();
            });
            _eventBus.Subscribe<SimulationTickCompletedEvent>(gameEvent => completedEvents++);
            _eventBus.Subscribe<SimulationStoppedEvent>(gameEvent => stoppedEvents++);

            _manager.Tick(0.5);

            Assert.AreEqual(1, tickEvents);
            Assert.AreEqual(0, completedEvents, "No completion may be published after a stop.");
            Assert.AreEqual(1, stoppedEvents);
            Assert.AreEqual(SimulationStatus.Stopped, _manager.Status);
            Assert.AreEqual(0L, _manager.TickCount);
            Assert.AreEqual(string.Empty, _manager.SessionId);
        }

        [Test]
        public void ResetDuringTickEvent_PublishesNoStaleTicks()
        {
            ConfigureFastClock();
            Start();

            int tickEvents = 0;
            int completedEvents = 0;
            _eventBus.Subscribe<SimulationTickEvent>(gameEvent =>
            {
                tickEvents++;
                _manager.Reset();
            });
            _eventBus.Subscribe<SimulationTickCompletedEvent>(gameEvent => completedEvents++);

            _manager.Tick(0.5);

            Assert.AreEqual(1, tickEvents);
            Assert.AreEqual(0, completedEvents);
            Assert.AreEqual(SimulationStatus.Stopped, _manager.Status);
            Assert.AreEqual(0L, _manager.TickCount);
            Assert.AreEqual(0.0, _manager.ElapsedSeconds, 0.0001);
        }

        [Test]
        public void DisposeDuringTickEvent_AbortsWithoutStaleEvents()
        {
            ConfigureFastClock();
            Start();

            int tickEvents = 0;
            int completedEvents = 0;
            _eventBus.Subscribe<SimulationTickEvent>(gameEvent =>
            {
                tickEvents++;
                _manager.Dispose();
            });
            _eventBus.Subscribe<SimulationTickCompletedEvent>(gameEvent => completedEvents++);

            _manager.Tick(0.5);

            Assert.AreEqual(1, tickEvents);
            Assert.AreEqual(0, completedEvents);
            Assert.AreEqual(SimulationStatus.Stopped, _manager.Status);
            Assert.AreEqual(0L, _manager.TickCount);

            Assert.DoesNotThrow(() => _manager.Tick(0.1));
            Assert.AreEqual(1, tickEvents, "A disposed manager must not tick again.");
        }

        [Test]
        public void NestedTickFromTickHandler_IsIgnored()
        {
            ConfigureFastClock();
            Start();

            int tickEvents = 0;
            bool nested = false;
            _eventBus.Subscribe<SimulationTickEvent>(gameEvent =>
            {
                tickEvents++;
                if (!nested)
                {
                    nested = true;
                    _manager.Tick(1.0);
                }
            });

            _manager.Tick(0.2);

            Assert.AreEqual(2, tickEvents, "A re-entrant Tick call must be ignored.");
            Assert.AreEqual(2L, _manager.TickCount);
            Assert.AreEqual(0.2, _manager.ElapsedSeconds, 0.0001);
        }

        [Test]
        public void PauseAndResumeDuringTickEvent_DoesNotDispatchTheRestOfTheBatch()
        {
            ConfigureFastClock();
            Start();

            int tickEvents = 0;
            int completedEvents = 0;
            _eventBus.Subscribe<SimulationTickEvent>(gameEvent =>
            {
                tickEvents++;
                _manager.Pause();
                _manager.Resume();
            });
            _eventBus.Subscribe<SimulationTickCompletedEvent>(gameEvent => completedEvents++);

            _manager.Tick(0.5);

            Assert.AreEqual(1, tickEvents);
            Assert.AreEqual(0, completedEvents);
            Assert.AreEqual(SimulationStatus.Running, _manager.Status);
            Assert.AreEqual(1L, _manager.TickCount);
            Assert.AreEqual(0.1, _manager.ElapsedSeconds, 0.0001);
        }

        [Test]
        public void StopAndRestartDuringTickEvent_DoesNotDispatchIntoTheNewSession()
        {
            ConfigureFastClock();

            int startedEvents = 0;
            int stoppedEvents = 0;
            int tickEvents = 0;
            int completedEvents = 0;

            _eventBus.Subscribe<SimulationStartedEvent>(gameEvent => startedEvents++);
            _eventBus.Subscribe<SimulationStoppedEvent>(gameEvent => stoppedEvents++);
            _eventBus.Subscribe<SimulationTickCompletedEvent>(gameEvent => completedEvents++);
            _eventBus.Subscribe<SimulationTickEvent>(gameEvent =>
            {
                tickEvents++;
                if (tickEvents > 1)
                {
                    return;
                }

                _manager.Stop();
                string error;
                _manager.TryStartSimulation(CreateSessionContext("session-two"), out error);
            });

            Start("session-one");
            _manager.Tick(0.5);

            Assert.AreEqual(2, startedEvents);
            Assert.AreEqual(1, stoppedEvents);
            Assert.AreEqual(1, tickEvents);
            Assert.AreEqual(0, completedEvents);
            Assert.AreEqual("session-two", _manager.SessionId);
            Assert.AreEqual(SimulationStatus.Running, _manager.Status);
            Assert.AreEqual(0L, _manager.TickCount, "The new session must not inherit the old batch.");
        }

        private void ConfigureFastClock()
        {
            _manager.Configure(new SimulationSettings(0.1, 8, 1.0, new[] { 1.0 }, 1.0, false));
        }

        [Test]
        public void Dispose_StopsSimulation_AndShutsSystemsDown()
        {
            RecordingSystem system = new RecordingSystem("alpha");
            _manager.Systems.Register(system);
            Start();
            _manager.Tick(Interval);

            _manager.Dispose();

            Assert.AreEqual(1, system.ShutdownCount);
            Assert.AreEqual(0L, _manager.TickCount);
            Assert.AreEqual(SimulationStatus.Stopped, _manager.Status);
        }
    }
}

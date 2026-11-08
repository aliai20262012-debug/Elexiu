using System.Collections.Generic;
using NUnit.Framework;
using Nexus.Configuration;
using Nexus.Core.Events;
using Nexus.Simulation;

namespace Nexus.Tests.EditMode
{
    /// <summary>
    /// Session lifecycle tests. They cover creation, duplicate prevention, name/seed rules,
    /// progress tracking, pause/resume, external stops and termination, including the
    /// guarantee that a failed simulation start never leaves a zombie session.
    /// </summary>
    public class GameSessionServiceTests
    {
        private EventBus _eventBus;
        private IConfigurationService _configuration;
        private SimulationManager _simulation;
        private GameSessionService _sessions;
        private List<GameSessionCreatedEvent> _created;
        private List<GameSessionEndedEvent> _ended;
        private int _seedCounter;

        [SetUp]
        public void SetUp()
        {
            _eventBus = new EventBus();
            _configuration = TestAssets.CreateConfigurationService();
            _simulation = new SimulationManager(_eventBus);
            _simulation.Configure(SimulationSettings.FromConfiguration(_configuration.Game));

            _seedCounter = 7000;
            _sessions = new GameSessionService(
                _eventBus,
                _configuration,
                _simulation,
                () => "session-" + System.Guid.NewGuid().ToString("N").Substring(0, 8),
                () => _seedCounter);

            _created = new List<GameSessionCreatedEvent>();
            _ended = new List<GameSessionEndedEvent>();
            _eventBus.Subscribe<GameSessionCreatedEvent>(gameEvent => _created.Add(gameEvent));
            _eventBus.Subscribe<GameSessionEndedEvent>(gameEvent => _ended.Add(gameEvent));
        }

        [TearDown]
        public void TearDown()
        {
            _sessions.Dispose();
            _simulation.Dispose();
            TestAssets.DestroyAll();
        }

        /// <summary>
        /// Advances exactly one tick. Frame deltas are split because the configured frame
        /// delta clamp (0.25s) is half the default tick interval (0.5s).
        /// </summary>
        private void AdvanceOneTick()
        {
            float half = _simulation.TickIntervalSeconds / 2f;
            _simulation.Tick(half);
            _simulation.Tick(half);
        }

        private GameSession CreateSession(string name = "AURORA", int seed = 0)
        {
            GameSession session;
            string error;
            bool created = _sessions.TryCreateSession(name, seed, out session, out error);
            Assert.IsTrue(created, "Session should be created: " + error);
            return session;
        }

        [Test]
        public void TryCreateSession_StartsSimulation_AndPublishesEvent()
        {
            GameSession session = CreateSession("AURORA", 1234);

            Assert.IsNotNull(session);
            Assert.AreEqual("AURORA", session.CivilizationName);
            Assert.AreEqual(1234, session.Seed);
            Assert.AreEqual(GameSessionState.Active, session.State);
            Assert.AreEqual(session.SessionId, _simulation.SessionId);
            Assert.IsTrue(_simulation.IsRunning);
            Assert.IsTrue(_sessions.HasActiveSession);

            Assert.AreEqual(1, _created.Count);
            Assert.AreEqual(session.SessionId, _created[0].SessionId);
            Assert.AreEqual("AURORA", _created[0].CivilizationName);
            Assert.AreEqual(1234, _created[0].Seed);
        }

        [Test]
        public void TryCreateSession_RejectsSecondActiveSession()
        {
            CreateSession();

            GameSession second;
            string error;
            bool created = _sessions.TryCreateSession("OTHER", 1, out second, out error);

            Assert.IsFalse(created);
            Assert.IsNull(second);
            StringAssert.Contains("already active", error);
            Assert.AreEqual(1, _created.Count);
        }

        [Test]
        public void TryCreateSession_GeneratesSeed_WhenZero()
        {
            GameSession session = CreateSession("AURORA", 0);

            Assert.AreEqual(_seedCounter, session.Seed);
        }

        [Test]
        public void TryCreateSession_UsesConfiguredName_WhenBlank()
        {
            GameSession session = CreateSession("   ", 5);

            Assert.AreEqual(_configuration.Game.DefaultCivilizationName, session.CivilizationName);
        }

        [Test]
        public void TryCreateSession_TruncatesOverlongName()
        {
            string longName = new string('X', 200);

            GameSession session = CreateSession(longName, 5);

            Assert.LessOrEqual(session.CivilizationName.Length, GameSession.MaximumNameLength);
        }

        [Test]
        public void TryCreateSession_ReportsInitializationFailure_AndLeavesNoSession()
        {
            _simulation.Systems.Register(new FailingInitializeSystem());

            GameSession session;
            string error;
            bool created = _sessions.TryCreateSession("AURORA", 1, out session, out error);

            Assert.IsFalse(created);
            Assert.IsNull(session);
            Assert.IsNull(_sessions.Current);
            Assert.IsFalse(_sessions.HasActiveSession);
            Assert.IsFalse(string.IsNullOrEmpty(error));
            Assert.AreEqual(SimulationStatus.Failed, _simulation.Status);

            Assert.AreEqual(1, _created.Count);
            Assert.AreEqual(1, _ended.Count);
            Assert.AreEqual(SessionEndReason.InitializationFailed, _ended[0].Reason);
        }

        private sealed class FailingInitializeSystem : ISimulationSystem
        {
            public string SystemId
            {
                get { return "failing-initialize"; }
            }

            public int ExecutionOrder
            {
                get { return 0; }
            }

            public IReadOnlyList<string> Dependencies
            {
                get { return new string[0]; }
            }

            public void Initialize(SimulationInitializationContext context)
            {
                throw new System.InvalidOperationException("boom-init");
            }

            public void Tick(SimulationContext context)
            {
            }

            public void Shutdown()
            {
            }
        }

        [Test]
        public void PauseAndResume_KeepSessionAndSimulationInSync()
        {
            GameSession session = CreateSession();

            Assert.IsTrue(_sessions.Pause());
            Assert.AreEqual(GameSessionState.Paused, session.State);
            Assert.IsTrue(_simulation.IsPaused);
            Assert.AreEqual(SimulationStatus.Paused, _simulation.Status);

            Assert.IsFalse(_sessions.Pause(), "Pausing twice must be rejected.");

            Assert.IsTrue(_sessions.Resume());
            Assert.AreEqual(GameSessionState.Active, session.State);
            Assert.AreEqual(SimulationStatus.Running, _simulation.Status);
        }

        [Test]
        public void PauseResume_ReturnFalseWithoutSession()
        {
            Assert.IsFalse(_sessions.Pause());
            Assert.IsFalse(_sessions.Resume());
            Assert.IsFalse(_sessions.SetSimulationSpeed(2.0));
        }

        [Test]
        public void SetSimulationSpeed_RequiresAnActiveSession()
        {
            CreateSession();

            Assert.IsTrue(_sessions.SetSimulationSpeed(2.0));
            Assert.AreEqual(2.0, _simulation.SpeedMultiplier, 0.0001);
            Assert.IsFalse(_sessions.SetSimulationSpeed(double.NaN));
        }

        [Test]
        public void TickCompleted_UpdatesSessionProgress()
        {
            GameSession session = CreateSession();
            double interval = _simulation.TickIntervalSeconds;

            AdvanceOneTick();
            Assert.AreEqual(1L, session.TickCount);
            Assert.AreEqual(interval, session.ElapsedSeconds, 0.0001);

            AdvanceOneTick();
            Assert.AreEqual(2L, session.TickCount);
            Assert.AreEqual(interval * 2.0, session.ElapsedSeconds, 0.0001);
        }

        [Test]
        public void EndSession_StopsSimulation_AndPublishesEndedEvent()
        {
            GameSession session = CreateSession();
            double interval = _simulation.TickIntervalSeconds;
            AdvanceOneTick();

            bool ended = _sessions.EndSession(SessionEndReason.PlayerRequested);

            Assert.IsTrue(ended);
            Assert.IsNull(_sessions.Current);
            Assert.IsFalse(_sessions.HasActiveSession);
            Assert.AreEqual(GameSessionState.None, _sessions.State);
            Assert.AreEqual(SimulationStatus.Stopped, _simulation.Status);

            Assert.AreEqual(1, _ended.Count);
            Assert.AreEqual(session.SessionId, _ended[0].SessionId);
            Assert.AreEqual(SessionEndReason.PlayerRequested, _ended[0].Reason);
            Assert.AreEqual(1L, _ended[0].TickCount);
            Assert.AreEqual(interval, _ended[0].ElapsedSeconds, 0.0001);
        }

        [Test]
        public void EndSession_IsIdempotent()
        {
            CreateSession();

            Assert.IsTrue(_sessions.EndSession(SessionEndReason.PlayerRequested));
            Assert.IsFalse(_sessions.EndSession(SessionEndReason.PlayerRequested));
            Assert.AreEqual(1, _ended.Count);
        }

        [Test]
        public void ExternalStop_ClosesTheSession()
        {
            GameSession session = CreateSession();

            _simulation.Stop();

            Assert.IsNull(_sessions.Current);
            Assert.IsFalse(_sessions.HasActiveSession);
            Assert.AreEqual(1, _ended.Count);
            Assert.AreEqual(session.SessionId, _ended[0].SessionId);
            Assert.AreEqual(SessionEndReason.Unknown, _ended[0].Reason);
        }

        [Test]
        public void Dispose_EndsActiveSession()
        {
            GameSession session = CreateSession();

            _sessions.Dispose();

            Assert.IsNull(_sessions.Current);
            Assert.AreEqual(1, _ended.Count);
            Assert.AreEqual(session.SessionId, _ended[0].SessionId);
            Assert.AreEqual(SessionEndReason.ApplicationQuit, _ended[0].Reason);
            Assert.AreEqual(SimulationStatus.Stopped, _simulation.Status);
        }

        [Test]
        public void SessionsCanBeRecreatedAfterEnding()
        {
            GameSession first = CreateSession();
            _sessions.EndSession(SessionEndReason.PlayerRequested);

            GameSession second = CreateSession("SECOND", 99);

            Assert.AreNotEqual(first.SessionId, second.SessionId);
            Assert.AreEqual("SECOND", second.CivilizationName);
            Assert.AreEqual(99, second.Seed);
            Assert.AreEqual(2, _created.Count);
        }
    }
}

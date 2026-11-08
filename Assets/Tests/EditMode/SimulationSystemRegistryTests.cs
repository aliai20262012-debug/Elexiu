using System;
using System.Collections.Generic;
using NUnit.Framework;
using Nexus.Core.Events;
using Nexus.Simulation;

namespace Nexus.Tests.EditMode
{
    /// <summary>
    /// Ordering, lifecycle and failure isolation tests for the system graph. These are the
    /// guarantees future planet/economy/AI systems will depend on.
    /// </summary>
    public class SimulationSystemRegistryTests
    {
        private sealed class FakeSystem : ISimulationSystem
        {
            private readonly List<string> _log;

            public FakeSystem(
                string systemId,
                List<string> log,
                int executionOrder = 0,
                string[] dependencies = null)
            {
                SystemId = systemId;
                ExecutionOrder = executionOrder;
                Dependencies = dependencies ?? new string[0];
                _log = log;
            }

            public string SystemId { get; }

            public int ExecutionOrder { get; }

            public IReadOnlyList<string> Dependencies { get; }

            public bool ThrowOnInitialize { get; set; }

            public bool ThrowOnTick { get; set; }

            public bool ThrowOnShutdown { get; set; }

            public int InitializeCount { get; private set; }

            public int ShutdownCount { get; private set; }

            public int TickCount { get; private set; }

            public SimulationContext LastContext { get; private set; }

            public void Initialize(SimulationInitializationContext context)
            {
                if (ThrowOnInitialize)
                {
                    throw new InvalidOperationException("boom-init");
                }

                InitializeCount++;
                if (_log != null)
                {
                    _log.Add("init:" + SystemId);
                }
            }

            public void Tick(SimulationContext context)
            {
                if (ThrowOnTick)
                {
                    throw new InvalidOperationException("boom-tick");
                }

                TickCount++;
                LastContext = context;
                if (_log != null)
                {
                    _log.Add("tick:" + SystemId);
                }
            }

            public void Shutdown()
            {
                if (ThrowOnShutdown)
                {
                    throw new InvalidOperationException("boom-shutdown");
                }

                ShutdownCount++;
                if (_log != null)
                {
                    _log.Add("shutdown:" + SystemId);
                }
            }
        }

        private SimulationInitializationContext _context;

        [SetUp]
        public void SetUp()
        {
            _context = new SimulationInitializationContext("session-1", 42, "TEST", 0.25, 1.0);
        }

        private static SimulationContext Tick(long tick)
        {
            return new SimulationContext(tick, 0.25, tick * 0.25, 1.0, "session-1");
        }

        [Test]
        public void Register_RejectsDuplicatesAndInvalidSystems()
        {
            SimulationSystemRegistry registry = new SimulationSystemRegistry();
            List<string> log = new List<string>();

            Assert.IsTrue(registry.Register(new FakeSystem("alpha", log)));
            Assert.IsFalse(registry.Register(new FakeSystem("alpha", log)), "Duplicate ids must be rejected.");
            Assert.IsFalse(registry.Register(null));
            Assert.IsFalse(registry.Register(new FakeSystem(string.Empty, log)));
            Assert.AreEqual(1, registry.Count);
        }

        [Test]
        public void InitializeAll_OrdersDependenciesBeforeDependants()
        {
            List<string> log = new List<string>();
            SimulationSystemRegistry registry = new SimulationSystemRegistry();

            registry.Register(new FakeSystem("civilization", log, 0, new[] { "planet" }));
            registry.Register(new FakeSystem("planet", log));
            registry.Register(new FakeSystem("economy", log, 0, new[] { "civilization" }));

            SystemInitializationReport report = registry.InitializeAll(_context);

            Assert.IsTrue(report.Succeeded);
            Assert.AreEqual(new[] { "planet", "civilization", "economy" }, report.InitializationOrder);
            CollectionAssert.AreEqual(new[] { "init:planet", "init:civilization", "init:economy" }, log);
        }

        [Test]
        public void InitializeAll_UsesExecutionOrderForIndependentSystems()
        {
            List<string> log = new List<string>();
            SimulationSystemRegistry registry = new SimulationSystemRegistry();

            registry.Register(new FakeSystem("late", log, 100));
            registry.Register(new FakeSystem("early", log, -100));

            SystemInitializationReport report = registry.InitializeAll(_context);

            Assert.IsTrue(report.Succeeded);
            Assert.AreEqual(new[] { "early", "late" }, report.InitializationOrder);
        }

        [Test]
        public void InitializeAll_BreaksTiesByIdentifier()
        {
            SimulationSystemRegistry registry = new SimulationSystemRegistry();
            registry.Register(new FakeSystem("zeta", null));
            registry.Register(new FakeSystem("alpha", null));

            SystemInitializationReport report = registry.InitializeAll(_context);

            Assert.AreEqual(new[] { "alpha", "zeta" }, report.InitializationOrder);
        }

        [Test]
        public void InitializeAll_ReportsMissingDependency_WithoutInitializingAnything()
        {
            List<string> log = new List<string>();
            SimulationSystemRegistry registry = new SimulationSystemRegistry();
            registry.Register(new FakeSystem("economy", log, 0, new[] { "ghost" }));

            SystemInitializationReport report = registry.InitializeAll(_context);

            Assert.IsFalse(report.Succeeded);
            Assert.AreEqual(1, report.Failures.Count);
            Assert.AreEqual("economy", report.Failures[0].SystemId);
            Assert.AreEqual("Ordering", report.Failures[0].Phase);
            StringAssert.Contains("ghost", report.Failures[0].Message);
            Assert.AreEqual(0, log.Count);
            Assert.IsFalse(registry.IsInitialized);
        }

        [Test]
        public void InitializeAll_DetectsDependencyCycles()
        {
            SimulationSystemRegistry registry = new SimulationSystemRegistry();
            registry.Register(new FakeSystem("alpha", null, 0, new[] { "beta" }));
            registry.Register(new FakeSystem("beta", null, 0, new[] { "alpha" }));

            SystemInitializationReport report = registry.InitializeAll(_context);

            Assert.IsFalse(report.Succeeded);
            Assert.AreEqual(1, report.Failures.Count);
            StringAssert.Contains("cycle", report.Failures[0].Message);
            Assert.IsFalse(registry.IsInitialized);
        }

        [Test]
        public void InitializeAll_RollsBack_WhenSystemThrows()
        {
            List<string> log = new List<string>();
            SimulationSystemRegistry registry = new SimulationSystemRegistry();

            FakeSystem first = new FakeSystem("alpha", log);
            FakeSystem failing = new FakeSystem("beta", log) { ThrowOnInitialize = true };
            FakeSystem third = new FakeSystem("gamma", log);

            registry.Register(first);
            registry.Register(failing);
            registry.Register(third);

            SystemInitializationReport report = registry.InitializeAll(_context);

            Assert.IsFalse(report.Succeeded);
            Assert.AreEqual("beta", report.Failures[0].SystemId);
            Assert.AreEqual("Initialize", report.Failures[0].Phase);
            Assert.AreEqual(1, first.InitializeCount);
            Assert.AreEqual(1, first.ShutdownCount, "Already initialized systems must be rolled back.");
            Assert.AreEqual(0, third.InitializeCount, "Systems after the failure must not initialize.");
            Assert.IsFalse(registry.IsInitialized);
            Assert.Greater(log.Count, 0);
        }

        [Test]
        public void InitializeAll_IsIdempotent()
        {
            List<string> log = new List<string>();
            SimulationSystemRegistry registry = new SimulationSystemRegistry();
            FakeSystem system = new FakeSystem("alpha", log);
            registry.Register(system);

            registry.InitializeAll(_context);
            SystemInitializationReport second = registry.InitializeAll(_context);

            Assert.IsTrue(second.Succeeded);
            Assert.AreEqual(1, system.InitializeCount);
        }

        [Test]
        public void Register_WhileInitialized_IsRejected()
        {
            SimulationSystemRegistry registry = new SimulationSystemRegistry();
            registry.Register(new FakeSystem("alpha", null));
            registry.InitializeAll(_context);

            Assert.IsFalse(registry.Register(new FakeSystem("beta", null)));
            Assert.AreEqual(1, registry.Count);
        }

        [Test]
        public void TickAll_DispatchesInExecutionOrder_WithContext()
        {
            List<string> log = new List<string>();
            SimulationSystemRegistry registry = new SimulationSystemRegistry();
            FakeSystem second = new FakeSystem("second", log, 20, new[] { "first" });
            FakeSystem first = new FakeSystem("first", log, 10);

            registry.Register(second);
            registry.Register(first);
            registry.InitializeAll(_context);
            log.Clear();

            int failures = registry.TickAll(Tick(7));

            Assert.AreEqual(0, failures);
            CollectionAssert.AreEqual(new[] { "tick:first", "tick:second" }, log);
            Assert.AreEqual(7L, second.LastContext.Tick);
            Assert.AreEqual(0.25, second.LastContext.DeltaSeconds, 0.0001);
        }

        [Test]
        public void TickAll_BeforeInitialization_DoesNothing()
        {
            FakeSystem system = new FakeSystem("alpha", null);
            SimulationSystemRegistry registry = new SimulationSystemRegistry();
            registry.Register(system);

            Assert.AreEqual(0, registry.TickAll(Tick(1)));
            Assert.AreEqual(0, system.TickCount);
        }

        [Test]
        public void TickAll_IsolatesFailingSystem()
        {
            SimulationSystemRegistry registry = new SimulationSystemRegistry();
            FakeSystem failing = new FakeSystem("alpha", null) { ThrowOnTick = true };
            FakeSystem healthy = new FakeSystem("beta", null);

            registry.Register(failing);
            registry.Register(healthy);
            registry.InitializeAll(_context);

            int failures = registry.TickAll(Tick(1));

            Assert.AreEqual(1, failures);
            Assert.AreEqual(1, healthy.TickCount, "A failing system must not stop its peers.");
        }

        [Test]
        public void TickAll_DisablesChronicallyFailingSystem()
        {
            SimulationSystemRegistry registry = new SimulationSystemRegistry();
            FakeSystem failing = new FakeSystem("alpha", null) { ThrowOnTick = true };
            registry.Register(failing);
            registry.InitializeAll(_context);

            for (int i = 0; i < SimulationSystemRegistry.MaxConsecutiveFailures; i++)
            {
                Assert.AreEqual(1, registry.TickAll(Tick(i + 1)));
            }

            Assert.IsTrue(registry.IsDisabled("alpha"));
            Assert.AreEqual(0, registry.TickAll(Tick(100)), "A disabled system is no longer ticked.");
        }

        [Test]
        public void ShutdownAll_ReversesInitializationOrder_AndIsIdempotent()
        {
            List<string> log = new List<string>();
            SimulationSystemRegistry registry = new SimulationSystemRegistry();
            registry.Register(new FakeSystem("alpha", log));
            registry.Register(new FakeSystem("beta", log, 0, new[] { "alpha" }));
            registry.InitializeAll(_context);
            log.Clear();

            registry.ShutdownAll();
            registry.ShutdownAll();

            CollectionAssert.AreEqual(new[] { "shutdown:beta", "shutdown:alpha" }, log);
            Assert.IsFalse(registry.IsInitialized);
        }

        [Test]
        public void ShutdownAll_SurvivesFailingShutdown()
        {
            List<string> log = new List<string>();
            SimulationSystemRegistry registry = new SimulationSystemRegistry();
            registry.Register(new FakeSystem("alpha", log));
            registry.Register(new FakeSystem("beta", log) { ThrowOnShutdown = true });
            registry.InitializeAll(_context);

            Assert.DoesNotThrow(registry.ShutdownAll);
            Assert.IsFalse(registry.IsInitialized);
        }

        [Test]
        public void InitializationFailure_IsPublishedOnTheEventBus()
        {
            EventBus bus = new EventBus();
            SimulationSystemRegistry registry = new SimulationSystemRegistry(bus);
            registry.Register(new FakeSystem("alpha", null) { ThrowOnInitialize = true });

            string capturedId = null;
            string capturedPhase = null;
            bus.Subscribe<SimulationSystemFailedEvent>(gameEvent =>
            {
                capturedId = gameEvent.SystemId;
                capturedPhase = gameEvent.Phase;
            });

            registry.InitializeAll(_context);

            Assert.AreEqual("alpha", capturedId);
            Assert.AreEqual("Initialize", capturedPhase);
        }

        [Test]
        public void Clear_RemovesRegistrations_AndShutsSystemsDown()
        {
            List<string> log = new List<string>();
            SimulationSystemRegistry registry = new SimulationSystemRegistry();
            registry.Register(new FakeSystem("alpha", log));
            registry.InitializeAll(_context);

            registry.Clear();

            Assert.AreEqual(0, registry.Count);
            Assert.IsFalse(registry.IsInitialized);
            CollectionAssert.Contains(log, "shutdown:alpha");
        }
    }
}

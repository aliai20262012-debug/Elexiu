using NUnit.Framework;
using Nexus.Core.Events;
using Nexus.Planet;
using Nexus.Simulation;
using UnityEngine;

namespace Nexus.Tests.EditMode
{
    /// <summary>
    /// Lifecycle, determinism and event behaviour of the planet simulation system. The system
    /// must work headless: no scene, no camera, no UI.
    /// </summary>
    public class PlanetSystemTests
    {
        private PlanetDefinition _definition;

        [SetUp]
        public void SetUp()
        {
            _definition = PlanetDefinition.CreateRuntimeDefault();
        }

        [TearDown]
        public void TearDown()
        {
            if (_definition != null)
            {
                Object.DestroyImmediate(_definition);
            }
        }

        private static SimulationInitializationContext InitContext(int seed)
        {
            return new SimulationInitializationContext("session-planet", seed, "AURORA", 0.5, 1.0);
        }

        private static SimulationContext Tick(long tick, double elapsed)
        {
            return new SimulationContext(tick, 0.5, elapsed, 1.0, "session-planet");
        }

        [Test]
        public void Identity_IsStable()
        {
            PlanetSystem system = new PlanetSystem(_definition);

            Assert.AreEqual("planet", system.SystemId);
            Assert.AreEqual(PlanetSystem.SystemOrder, system.ExecutionOrder);
            Assert.AreEqual(0, system.Dependencies.Count);
        }

        [Test]
        public void Initialize_BuildsSurfaceStateAndRegions()
        {
            PlanetSystem system = new PlanetSystem(_definition);

            system.Initialize(InitContext(42));

            Assert.IsTrue(system.IsInitialized);
            Assert.IsTrue(system.HasState);
            Assert.IsNotNull(system.Surface);
            Assert.IsNotNull(system.State);
            Assert.AreEqual(_definition.Regions.Count, system.Regions.Count);
        }

        [Test]
        public void Tick_AdvancesThePlanetCalendar()
        {
            PlanetSystem system = new PlanetSystem(_definition);
            system.Initialize(InitContext(1));

            double dayLength = _definition.DayLengthSeconds;
            system.Tick(Tick(1, dayLength * 0.25));
            Assert.AreEqual(0, system.State.DayNumber);
            Assert.AreEqual(0.25, system.State.PlanetDays, 0.001);

            system.Tick(Tick(2, dayLength * 1.5));
            Assert.AreEqual(1, system.State.DayNumber);
        }

        [Test]
        public void Tick_PublishesEnvironmentOncePerSimulatedDay()
        {
            EventBus eventBus = new EventBus();
            int published = 0;
            PlanetEnvironmentUpdatedEvent last = default(PlanetEnvironmentUpdatedEvent);
            eventBus.Subscribe<PlanetEnvironmentUpdatedEvent>(gameEvent =>
            {
                published++;
                last = gameEvent;
            });

            PlanetSystem system = new PlanetSystem(_definition, eventBus);
            system.Initialize(InitContext(5));

            double dayLength = _definition.DayLengthSeconds;
            system.Tick(Tick(1, dayLength * 0.2));
            system.Tick(Tick(2, dayLength * 0.4));
            system.Tick(Tick(3, dayLength * 1.1));

            Assert.AreEqual(2, published, "One update per simulated day.");
            Assert.AreEqual(1, last.DayNumber);
            Assert.AreEqual(_definition.Regions.Count, last.RegionCount);
        }

        [Test]
        public void Tick_IsDeterministicForTheSameSeed()
        {
            PlanetSystem first = new PlanetSystem(_definition);
            PlanetSystem second = new PlanetSystem(_definition);

            first.Initialize(InitContext(777));
            second.Initialize(InitContext(777));

            for (int i = 1; i <= 8; i++)
            {
                first.Tick(Tick(i, i * 200.0));
                second.Tick(Tick(i, i * 200.0));
            }

            Assert.AreEqual(first.State.MeanTemperatureC, second.State.MeanTemperatureC, 0.0001f);

            Vector3 sample = PlanetGeo.DirectionFromLatLon(17f, -63f);
            Assert.AreEqual(first.Surface.HeightAt(sample), second.Surface.HeightAt(sample), 0.000001f);
        }

        [Test]
        public void TryGetRegionEnvironment_MatchesSharedTemperatureFormula()
        {
            PlanetSystem system = new PlanetSystem(_definition);
            system.Initialize(InitContext(31));
            system.Tick(Tick(1, _definition.DayLengthSeconds * 40.0));

            PlanetRegion region = system.Regions[0];
            PlanetRegionEnvironment environment;
            bool found = system.TryGetRegionEnvironment(region.Id, out environment);

            Assert.IsTrue(found);
            Assert.AreEqual(region.Id, environment.RegionId);
            Assert.AreEqual(
                PlanetSimulationState.TemperatureFor(region, system.State.SeasonPhase),
                environment.TemperatureC,
                0.0001f);
            Assert.AreEqual(region.Habitability.Score, environment.HabitabilityScore, 0.0001f);
        }

        [Test]
        public void TryGetRegionEnvironment_ReturnsFalseBeforeInitialize()
        {
            PlanetSystem system = new PlanetSystem(_definition);

            PlanetRegionEnvironment environment;
            Assert.IsFalse(system.TryGetRegionEnvironment(1, out environment));
        }

        [Test]
        public void TickBeforeInitialize_IsIgnored()
        {
            PlanetSystem system = new PlanetSystem(_definition);

            Assert.DoesNotThrow(() => system.Tick(Tick(1, 10.0)));
            Assert.IsFalse(system.HasState);
        }

        [Test]
        public void Shutdown_ReleasesSessionState()
        {
            PlanetSystem system = new PlanetSystem(_definition);
            system.Initialize(InitContext(8));

            system.Shutdown();

            Assert.IsFalse(system.IsInitialized);
            Assert.IsFalse(system.HasState);
            Assert.IsNull(system.Surface);
            Assert.IsNull(system.State);
        }

        [Test]
        public void System_RegistersOnTheSimulationGraph()
        {
            SimulationSystemRegistry registry = new SimulationSystemRegistry();
            PlanetSystem system = new PlanetSystem(_definition);

            Assert.IsTrue(registry.Register(system));
            SystemInitializationReport report = registry.InitializeAll(InitContext(12));

            Assert.IsTrue(report.Succeeded, report.DescribeFailures());
            Assert.AreEqual(1, report.InitializedCount);
            Assert.AreEqual("planet", registry.ExecutionOrder[0]);

            int failures = registry.TickAll(Tick(1, 500.0));
            Assert.AreEqual(0, failures);
            Assert.AreEqual(0, system.State.DayNumber);

            registry.ShutdownAll();
            Assert.IsFalse(registry.IsInitialized);
            Assert.IsFalse(system.IsInitialized);
        }

        [Test]
        public void SeasonPhase_AdvancesWithTheOrbitalPeriod()
        {
            PlanetSystem system = new PlanetSystem(_definition);
            system.Initialize(InitContext(3));

            double orbitalSeconds = _definition.OrbitalPeriodDays * _definition.DayLengthSeconds;
            system.Tick(Tick(1, orbitalSeconds * 0.25));

            Assert.AreEqual(0.25f, system.State.SeasonPhase, 0.005f);
            Assert.AreEqual("AUTUMN", system.State.SeasonLabel);
        }
    }
}

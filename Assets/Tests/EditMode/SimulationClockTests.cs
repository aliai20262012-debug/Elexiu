using NUnit.Framework;
using Nexus.Simulation;

namespace Nexus.Tests.EditMode
{
    /// <summary>
    /// Determinism and safety tests for the fixed-step clock. These run without a scene and
    /// without Unity frame time, which is exactly the contract the simulation relies on.
    /// </summary>
    public class SimulationClockTests
    {
        private const double Tolerance = 0.0001;

        private static SimulationClock CreateClock(double interval = 0.25, int catchUp = 8, double maxFrame = 0.25)
        {
            SimulationClock clock = new SimulationClock();
            clock.Configure(interval, catchUp, maxFrame);
            return clock;
        }

        [Test]
        public void NewClock_StartsClean()
        {
            SimulationClock clock = new SimulationClock();

            Assert.AreEqual(0L, clock.TickCount);
            Assert.AreEqual(0.0, clock.ElapsedSeconds, Tolerance);
            Assert.AreEqual(SimulationSpeed.Normal, clock.SpeedMultiplier, Tolerance);
            Assert.IsFalse(clock.IsPaused);
            Assert.AreEqual(SimulationClock.DefaultTickIntervalSeconds, clock.TickIntervalSeconds, Tolerance);
        }

        [Test]
        public void Advance_BelowInterval_ProducesNoTick()
        {
            SimulationClock clock = CreateClock(0.25);

            int ticks = clock.Advance(0.1);

            Assert.AreEqual(0, ticks);
            Assert.AreEqual(0L, clock.TickCount);
            Assert.Greater(clock.PendingTickFraction, 0.0);
        }

        [Test]
        public void Advance_AccumulatesAcrossFrames()
        {
            SimulationClock clock = CreateClock(1.0);

            int total = 0;
            for (int i = 0; i < 3; i++)
            {
                total += clock.Advance(0.25);
            }

            Assert.AreEqual(0, total, "Partial frames must accumulate without completing a tick.");
            Assert.AreEqual(1, clock.Advance(0.25));
            Assert.AreEqual(1L, clock.TickCount);
            Assert.AreEqual(1.0, clock.ElapsedSeconds, Tolerance);
        }

        [Test]
        public void Advance_ProducesMultipleTicks_ForLongFrame()
        {
            SimulationClock clock = CreateClock(0.1, 8, 1.0);

            int ticks = clock.Advance(0.35);

            Assert.AreEqual(3, ticks);
            Assert.AreEqual(3L, clock.TickCount);
            Assert.AreEqual(0.3, clock.ElapsedSeconds, Tolerance);
        }

        [Test]
        public void Advance_ClampsFrameDelta()
        {
            SimulationClock clock = CreateClock(0.1, 8, 0.25);

            int ticks = clock.Advance(10.0);

            Assert.AreEqual(2, ticks, "Only the clamped 0.25s may be simulated.");
            Assert.AreEqual(1L, clock.ClampedFrames);
            Assert.LessOrEqual(clock.ElapsedSeconds, 0.25);
        }

        [Test]
        public void Advance_DropsBacklogBeyondCatchUpBudget()
        {
            SimulationClock clock = CreateClock(0.1, 2, 1.0);

            int ticks = clock.Advance(0.5);

            Assert.AreEqual(2, ticks);
            Assert.AreEqual(2L, clock.TickCount);
            Assert.AreEqual(3L, clock.DroppedTicks, "The remaining three ticks must be discarded, not replayed later.");
            Assert.Less(clock.PendingTickFraction, 0.01);
        }

        [Test]
        public void Pause_ProducesNoTicks_AndResumeContinues()
        {
            SimulationClock clock = CreateClock(0.25);
            clock.SetPaused(true);

            Assert.AreEqual(0, clock.Advance(5.0));
            Assert.AreEqual(0L, clock.TickCount);

            clock.SetPaused(false);
            Assert.AreEqual(1, clock.Advance(0.25));
        }

        [Test]
        public void PausingDiscardsPartialTick()
        {
            SimulationClock clock = CreateClock(1.0);

            Assert.AreEqual(0, clock.Advance(0.75));
            clock.SetPaused(true);
            clock.SetPaused(false);

            Assert.AreEqual(0.0, clock.PendingTickFraction, Tolerance);
            Assert.AreEqual(0, clock.Advance(0.25), "Resume must not finish a tick accumulated before the pause.");
            Assert.AreEqual(1, clock.Advance(0.75));
        }

        [Test]
        public void SpeedMultiplier_ScalesAccumulation()
        {
            SimulationClock clock = CreateClock(1.0);
            clock.SetSpeed(4.0);

            Assert.AreEqual(1, clock.Advance(0.25));
            Assert.AreEqual(4.0, clock.SpeedMultiplier, Tolerance);
        }

        [Test]
        public void ElapsedSeconds_MatchesTickCountTimesInterval()
        {
            SimulationClock clock = CreateClock(0.25, 8, 1.0);

            clock.Advance(0.6);

            Assert.AreEqual(2L, clock.TickCount);
            Assert.AreEqual(0.5, clock.ElapsedSeconds, Tolerance);
        }

        [Test]
        public void Advance_IgnoresInvalidDeltas()
        {
            SimulationClock clock = CreateClock(0.25);

            Assert.AreEqual(0, clock.Advance(0.0));
            Assert.AreEqual(0, clock.Advance(-1.0));
            Assert.AreEqual(0, clock.Advance(double.NaN));
            Assert.AreEqual(0, clock.Advance(double.PositiveInfinity));
            Assert.AreEqual(0L, clock.TickCount);
        }

        [Test]
        public void Reset_ClearsProgressAndDiagnostics()
        {
            SimulationClock clock = CreateClock(0.1, 2, 1.0);
            clock.Advance(1.0);
            clock.SetPaused(true);

            clock.Reset();

            Assert.AreEqual(0L, clock.TickCount);
            Assert.AreEqual(0.0, clock.ElapsedSeconds, Tolerance);
            Assert.AreEqual(0L, clock.DroppedTicks);
            Assert.AreEqual(0L, clock.ClampedFrames);
            Assert.IsFalse(clock.IsPaused);
        }

        [Test]
        public void Configure_ClampsInvalidValues()
        {
            SimulationClock clock = new SimulationClock();

            clock.Configure(-10.0, 0, double.NaN);

            Assert.AreEqual(SimulationClock.MinimumTickIntervalSeconds, clock.TickIntervalSeconds, Tolerance);
            Assert.AreEqual(SimulationClock.MinimumCatchUpTicksPerFrame, clock.MaxCatchUpTicksPerFrame);
            Assert.AreEqual(0.25, clock.MaxFrameDeltaSeconds, Tolerance);

            clock.Configure(1000.0, 5000, 1000.0);

            Assert.AreEqual(SimulationClock.MaximumTickIntervalSeconds, clock.TickIntervalSeconds, Tolerance);
            Assert.AreEqual(SimulationClock.MaximumCatchUpTicksPerFrame, clock.MaxCatchUpTicksPerFrame);
            Assert.AreEqual(SimulationClock.MaximumFrameDeltaSeconds, clock.MaxFrameDeltaSeconds, Tolerance);
        }

        [Test]
        public void SameDeltaSequence_ProducesIdenticalTicks()
        {
            double[] frames = { 0.016, 0.033, 0.25, 0.5, 0.017, 0.017 };

            SimulationClock first = CreateClock(0.1, 8, 0.5);
            SimulationClock second = CreateClock(0.1, 8, 0.5);

            for (int pass = 0; pass < 20; pass++)
            {
                for (int i = 0; i < frames.Length; i++)
                {
                    first.Advance(frames[i]);
                    second.Advance(frames[i]);
                }
            }

            Assert.AreEqual(first.TickCount, second.TickCount);
            Assert.AreEqual(first.ElapsedSeconds, second.ElapsedSeconds, 0.0);
            Assert.AreEqual(first.DroppedTicks, second.DroppedTicks);
        }

        [Test]
        public void SpeedChanges_AreDeterministic()
        {
            SimulationClock first = CreateClock(0.25);
            SimulationClock second = CreateClock(0.25);

            for (int i = 0; i < 10; i++)
            {
                double speed = (i % 2 == 0) ? 2.0 : 4.0;
                first.SetSpeed(speed);
                second.SetSpeed(speed);
                first.Advance(0.1);
                second.Advance(0.1);
            }

            Assert.AreEqual(first.TickCount, second.TickCount);
            Assert.Greater(first.TickCount, 0L);
        }
    }
}

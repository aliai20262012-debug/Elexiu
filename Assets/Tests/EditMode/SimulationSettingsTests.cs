using NUnit.Framework;
using Nexus.Configuration;
using Nexus.Simulation;

namespace Nexus.Tests.EditMode
{
    /// <summary>
    /// Validation tests for simulation timing configuration. Bad designer input must be
    /// clamped, filtered or replaced with defaults - never throw and never reach the clock.
    /// </summary>
    public class SimulationSettingsTests
    {
        private const double Tolerance = 0.0001;

        [TearDown]
        public void TearDown()
        {
            TestAssets.DestroyAll();
        }

        [Test]
        public void DefaultSettings_AreUsable()
        {
            SimulationSettings settings = SimulationSettings.CreateDefault();

            Assert.AreEqual(0.5, settings.TickIntervalSeconds, Tolerance);
            Assert.AreEqual(8, settings.MaxCatchUpTicksPerFrame);
            Assert.AreEqual(0.25, settings.MaxFrameDeltaSeconds, Tolerance);
            Assert.AreEqual(3, settings.SpeedLevelCount);
            Assert.AreEqual(1.0, settings.SpeedLevels[0], Tolerance);
            Assert.AreEqual(2.0, settings.SpeedLevels[1], Tolerance);
            Assert.AreEqual(4.0, settings.SpeedLevels[2], Tolerance);
            Assert.AreEqual(1.0, settings.DefaultSpeedMultiplier, Tolerance);
            Assert.IsFalse(settings.StartPaused);
        }

        [Test]
        public void Settings_ClampOutOfRangeValues()
        {
            SimulationSettings settings = new SimulationSettings(
                -5.0,
                0,
                -2.0,
                new[] { 1.0 },
                -1.0,
                false);

            Assert.AreEqual(SimulationClock.MinimumTickIntervalSeconds, settings.TickIntervalSeconds, Tolerance);
            Assert.AreEqual(SimulationClock.MinimumCatchUpTicksPerFrame, settings.MaxCatchUpTicksPerFrame);
            Assert.AreEqual(SimulationClock.MinimumFrameDeltaSeconds, settings.MaxFrameDeltaSeconds, Tolerance);
            Assert.AreEqual(1.0, settings.DefaultSpeedMultiplier, Tolerance);

            SimulationSettings upper = new SimulationSettings(
                1000.0,
                5000,
                1000.0,
                new[] { 50.0, 100.0 },
                500.0,
                true);

            Assert.AreEqual(SimulationClock.MaximumTickIntervalSeconds, upper.TickIntervalSeconds, Tolerance);
            Assert.AreEqual(SimulationClock.MaximumCatchUpTicksPerFrame, upper.MaxCatchUpTicksPerFrame);
            Assert.AreEqual(SimulationClock.MaximumFrameDeltaSeconds, upper.MaxFrameDeltaSeconds, Tolerance);
            Assert.AreEqual(SimulationSpeed.Maximum, upper.DefaultSpeedMultiplier, Tolerance);
            Assert.IsTrue(upper.StartPaused);
        }

        [Test]
        public void Settings_FallBackToDefaultSpeedLadder()
        {
            Assert.AreEqual(3, new SimulationSettings(0.5, 8, 0.25, null, 1.0, false).SpeedLevelCount);
            Assert.AreEqual(3, new SimulationSettings(0.5, 8, 0.25, new double[0], 1.0, false).SpeedLevelCount);

            SimulationSettings allInvalid = new SimulationSettings(
                0.5,
                8,
                0.25,
                new[] { double.NaN, -1.0, 0.0, 1000.0 },
                1.0,
                false);

            Assert.AreEqual(3, allInvalid.SpeedLevelCount);
        }

        [Test]
        public void Settings_SortAndDeduplicateSpeedLevels()
        {
            SimulationSettings settings = new SimulationSettings(
                0.5,
                8,
                0.25,
                new[] { 4.0, 1.0, 4.0, 2.0 },
                1.0,
                false);

            Assert.AreEqual(3, settings.SpeedLevelCount);
            CollectionAssert.AreEqual(new[] { 1.0, 2.0, 4.0 }, settings.CopySpeedLevels());
        }

        [Test]
        public void Settings_DiscardNonFiniteAndOutOfRangeLevels()
        {
            SimulationSettings settings = new SimulationSettings(
                0.5,
                8,
                0.25,
                new[] { 1.0, double.NaN, double.PositiveInfinity, -3.0, 2.0, 5000.0 },
                1.0,
                false);

            CollectionAssert.AreEqual(new[] { 1.0, 2.0 }, settings.CopySpeedLevels());
        }

        [Test]
        public void Settings_SnapDefaultSpeedToConfiguredLevel()
        {
            SimulationSettings settings = new SimulationSettings(
                0.5,
                8,
                0.25,
                new[] { 1.0, 3.0 },
                2.9,
                false);

            Assert.AreEqual(3.0, settings.DefaultSpeedMultiplier, Tolerance);
        }

        [Test]
        public void Settings_CopySpeedLevels_IsDefensive()
        {
            SimulationSettings settings = SimulationSettings.CreateDefault();

            double[] copy = settings.CopySpeedLevels();
            copy[0] = 99.0;

            Assert.AreEqual(1.0, settings.SpeedLevels[0], Tolerance);
        }

        [Test]
        public void FromConfiguration_ReadsValidatedValues()
        {
            GameConfiguration configuration = TestAssets.CreateConfiguration();
            SimulationSettings settings = SimulationSettings.FromConfiguration(configuration);

            Assert.AreEqual(configuration.SimulationTickIntervalSeconds, settings.TickIntervalSeconds, Tolerance);
            Assert.AreEqual(configuration.MaxCatchUpTicksPerFrame, settings.MaxCatchUpTicksPerFrame);
            Assert.AreEqual(configuration.MaxFrameDeltaSeconds, settings.MaxFrameDeltaSeconds, Tolerance);
            Assert.AreEqual(configuration.DefaultSimulationSpeedMultiplier, settings.DefaultSpeedMultiplier, Tolerance);
            Assert.AreEqual(configuration.SimulationSpeedMultipliers.Count, settings.SpeedLevelCount);
            Assert.IsFalse(settings.StartPaused);
        }

        [Test]
        public void FromConfiguration_HandlesNull()
        {
            SimulationSettings settings = SimulationSettings.FromConfiguration(null);

            Assert.AreEqual(0.5, settings.TickIntervalSeconds, Tolerance);
            Assert.AreEqual(3, settings.SpeedLevelCount);
        }

        [Test]
        public void GameConfiguration_SanitizesSpeedMultipliers()
        {
            float[] sanitized = GameConfiguration.SanitizeSpeedMultipliers(
                new[] { 3f, 0f, float.NaN, 3f, 5f, 5000f });

            CollectionAssert.AreEqual(new[] { 3f, 5f }, sanitized);
        }

        [Test]
        public void GameConfiguration_SanitizesEmptySpeedMultipliers()
        {
            CollectionAssert.AreEqual(new[] { 1f, 2f, 4f }, GameConfiguration.SanitizeSpeedMultipliers(null));
            CollectionAssert.AreEqual(new[] { 1f, 2f, 4f }, GameConfiguration.SanitizeSpeedMultipliers(new float[0]));
            CollectionAssert.AreEqual(new[] { 1f, 2f, 4f }, GameConfiguration.SanitizeSpeedMultipliers(new[] { 0f, -1f }));
        }

        [Test]
        public void GameConfiguration_NearestSpeedHandlesMissingLevels()
        {
            Assert.AreEqual(1f, GameConfiguration.NearestConfiguredSpeed(null, 4f), Tolerance);
            Assert.AreEqual(2f, GameConfiguration.NearestConfiguredSpeed(new[] { 1f, 2f, 4f }, 3f), Tolerance);
        }

        [Test]
        public void GameConfiguration_ExposesUsableSimulationDefaults()
        {
            GameConfiguration configuration = TestAssets.CreateConfiguration();

            Assert.Greater(configuration.SimulationTickIntervalSeconds, 0f);
            Assert.AreEqual(3, configuration.SimulationSpeedMultipliers.Count);
            Assert.GreaterOrEqual(configuration.MaxCatchUpTicksPerFrame, 1);
            Assert.Greater(configuration.MaxFrameDeltaSeconds, 0f);
            Assert.IsFalse(string.IsNullOrEmpty(configuration.DefaultCivilizationName));
            Assert.IsFalse(configuration.SimulationStartsPaused);
        }

        [Test]
        public void SimulationSpeed_FormatsLabelsInvariantly()
        {
            Assert.AreEqual("1x", SimulationSpeed.FormatLabel(1.0));
            Assert.AreEqual("2.5x", SimulationSpeed.FormatLabel(2.5));
            Assert.AreEqual("4x", SimulationSpeed.FormatLabel(4.0));
        }

        [Test]
        public void SimulationSpeed_ClampsInvalidMultipliers()
        {
            Assert.AreEqual(SimulationSpeed.Normal, SimulationSpeed.Clamp(double.NaN), Tolerance);
            Assert.AreEqual(SimulationSpeed.Minimum, SimulationSpeed.Clamp(-10.0), Tolerance);
            Assert.AreEqual(SimulationSpeed.Maximum, SimulationSpeed.Clamp(10000.0), Tolerance);
            Assert.IsFalse(SimulationSpeed.IsValid(0.0));
            Assert.IsTrue(SimulationSpeed.IsValid(1.0));
        }
    }
}

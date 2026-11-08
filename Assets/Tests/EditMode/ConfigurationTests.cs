using NUnit.Framework;
using Nexus.Configuration;
using Nexus.Core.Scenes;

namespace Nexus.Tests.EditMode
{
    public class ConfigurationTests
    {
        [TearDown]
        public void TearDown()
        {
            TestAssets.DestroyAll();
        }

        [Test]
        public void DefaultConfiguration_HasUsableScenes()
        {
            GameConfiguration configuration = TestAssets.CreateConfiguration();

            Assert.AreEqual(SceneNames.Bootstrap, configuration.BootstrapScene);
            Assert.AreEqual(SceneNames.MainMenu, configuration.MainMenuScene);
            Assert.AreEqual(SceneNames.Gameplay, configuration.GameplayScene);
            Assert.IsFalse(string.IsNullOrEmpty(configuration.GameTitle));
            Assert.IsFalse(string.IsNullOrEmpty(configuration.BuildVersion));
        }

        [Test]
        public void DefaultConfiguration_HasSanePerformanceValues()
        {
            GameConfiguration configuration = TestAssets.CreateConfiguration();

            Assert.Greater(configuration.DesktopTargetFrameRate, 0);
            Assert.Greater(configuration.MobileTargetFrameRate, 0);
            Assert.LessOrEqual(configuration.MobileTargetFrameRate, configuration.DesktopTargetFrameRate);
            Assert.Greater(configuration.SimulationTickIntervalSeconds, 0f);
        }

        [Test]
        public void TargetFrameRateFor_SelectsByPlatform()
        {
            GameConfiguration configuration = TestAssets.CreateConfiguration();

            Assert.AreEqual(configuration.DesktopTargetFrameRate, configuration.TargetFrameRateFor(false));
            Assert.AreEqual(configuration.MobileTargetFrameRate, configuration.TargetFrameRateFor(true));
        }

        [Test]
        public void DefaultTheme_DefinesTheDarkSciFiPalette()
        {
            UiTheme theme = TestAssets.CreateTheme();

            Assert.Less(theme.BackgroundDeepNavy.r, 0.2f, "Background must stay a deep navy.");
            Assert.Greater(theme.PrimaryCyan.b, 0.8f, "Primary accent must stay electric cyan.");
            Assert.Greater(theme.AccentGreen.g, 0.7f, "Green accent must stay readable.");
            Assert.Greater(theme.TitleFontSize, theme.BodyFontSize);
        }

        [Test]
        public void ConfigurationService_FallsBackToDefaults_WhenAssetsAreMissing()
        {
            ConfigurationService service = new ConfigurationService(null, null);

            Assert.IsNotNull(service.Game);
            Assert.IsNotNull(service.Theme);
        }
    }
}

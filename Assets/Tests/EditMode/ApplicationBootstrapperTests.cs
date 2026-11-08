using System;
using NUnit.Framework;
using Nexus.Configuration;
using Nexus.Core;
using Nexus.Core.Bootstrap;
using Nexus.Core.Events;
using Nexus.Core.Platform;
using Nexus.Core.Scenes;
using Nexus.Core.State;
using Nexus.SaveSystem;
using Nexus.Settings;
using Nexus.Simulation;

namespace Nexus.Tests.EditMode
{
    public class ApplicationBootstrapperTests
    {
        private sealed class FakeSceneLoader : ISceneLoader
        {
            public bool IsLoading { get; private set; }

            public void Load(string sceneName, Action<SceneLoadResult> onCompleted)
            {
                if (onCompleted != null)
                {
                    onCompleted(SceneLoadResult.Success(sceneName));
                }
            }
        }

        private ServiceRegistry _registry;

        [TearDown]
        public void TearDown()
        {
            if (_registry != null)
            {
                _registry.Dispose();
                _registry = null;
            }

            if (AppContext.IsReady)
            {
                AppContext.Uninstall();
            }

            TestAssets.DestroyAll();
        }

        [Test]
        public void Compose_RegistersEveryCoreService()
        {
            EventBus eventBus = new EventBus();
            IConfigurationService configuration = TestAssets.CreateConfigurationService();
            PlatformInfo platform = new PlatformInfo(PlatformKind.WindowsDesktop, 96f, 24f);
            PlatformConfiguration platformConfiguration = PlatformConfiguration.For(platform);

            _registry = ApplicationBootstrapper.Compose(
                eventBus,
                configuration,
                platform,
                platformConfiguration,
                new SettingsService(eventBus, platformConfiguration),
                new NullSaveService(),
                new FakeSceneLoader(),
                new SimulationManager(eventBus));

            Assert.IsTrue(_registry.IsRegistered<IEventBus>());
            Assert.IsTrue(_registry.IsRegistered<IConfigurationService>());
            Assert.IsTrue(_registry.IsRegistered<IPlatformInfo>());
            Assert.IsTrue(_registry.IsRegistered<PlatformConfiguration>());
            Assert.IsTrue(_registry.IsRegistered<ISettingsService>());
            Assert.IsTrue(_registry.IsRegistered<ISaveService>());
            Assert.IsTrue(_registry.IsRegistered<ISceneLoader>());
            Assert.IsTrue(_registry.IsRegistered<ISimulationManager>());
            Assert.IsTrue(_registry.IsRegistered<IGameSessionService>());
            Assert.IsTrue(_registry.IsRegistered<GameManager>());
            Assert.AreEqual(10, _registry.Count);
        }

        [Test]
        public void Compose_ConfiguresSimulationFromProjectConfiguration()
        {
            EventBus eventBus = new EventBus();
            IConfigurationService configuration = TestAssets.CreateConfigurationService();
            PlatformConfiguration platformConfiguration = PlatformConfiguration.For(null);

            _registry = ApplicationBootstrapper.Compose(
                eventBus,
                configuration,
                new PlatformInfo(PlatformKind.WindowsDesktop, 96f, 24f),
                platformConfiguration,
                new SettingsService(eventBus, platformConfiguration),
                new NullSaveService(),
                new FakeSceneLoader(),
                new SimulationManager(eventBus));

            ISimulationManager simulation = _registry.Get<ISimulationManager>();

            Assert.AreEqual(configuration.Game.SimulationTickIntervalSeconds, simulation.TickIntervalSeconds, 0.0001);
            Assert.AreEqual(configuration.Game.DefaultSimulationSpeedMultiplier, simulation.SpeedMultiplier, 0.0001);
            Assert.AreEqual(configuration.Game.SimulationSpeedMultipliers.Count, simulation.Settings.SpeedLevelCount);
            Assert.IsNotNull(_registry.Get<IGameSessionService>());
        }

        [Test]
        public void Compose_ProducesInitializedGameManager()
        {
            EventBus eventBus = new EventBus();
            IConfigurationService configuration = TestAssets.CreateConfigurationService();
            PlatformInfo platform = new PlatformInfo(PlatformKind.AndroidTablet, 264f, 10f);
            PlatformConfiguration platformConfiguration = PlatformConfiguration.For(platform);

            _registry = ApplicationBootstrapper.Compose(
                eventBus,
                configuration,
                platform,
                platformConfiguration,
                new SettingsService(eventBus, platformConfiguration),
                new NullSaveService(),
                new FakeSceneLoader(),
                new SimulationManager(eventBus));

            GameManager gameManager = _registry.Get<GameManager>();
            gameManager.Initialize();

            Assert.AreEqual(GameState.Booting, gameManager.State);
        }

        [Test]
        public void Compose_RejectsMissingDependencies()
        {
            Assert.Throws<ArgumentNullException>(() => ApplicationBootstrapper.Compose(
                null,
                TestAssets.CreateConfigurationService(),
                null,
                null,
                null,
                null,
                new FakeSceneLoader(),
                null));
        }

        [Test]
        public void AppContext_InstallExposesServices()
        {
            EventBus eventBus = new EventBus();
            IConfigurationService configuration = TestAssets.CreateConfigurationService();
            PlatformConfiguration platformConfiguration = PlatformConfiguration.For(null);

            _registry = ApplicationBootstrapper.Compose(
                eventBus,
                configuration,
                new PlatformInfo(PlatformKind.WindowsDesktop, 96f, 24f),
                platformConfiguration,
                new SettingsService(eventBus, platformConfiguration),
                new NullSaveService(),
                new FakeSceneLoader(),
                new SimulationManager(eventBus));

            AppContext.Install(_registry);

            Assert.IsTrue(AppContext.IsReady);
            Assert.IsTrue(AppContext.TryGet(out GameManager resolved));
            Assert.IsNotNull(resolved);
        }
    }
}

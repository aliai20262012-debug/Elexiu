using System;
using Nexus.Configuration;
using Nexus.Core.Events;
using Nexus.Core.Logging;
using Nexus.Core.Platform;
using Nexus.Core.Scenes;
using Nexus.Planet;
using Nexus.SaveSystem;
using Nexus.Settings;
using Nexus.Simulation;
using UnityEngine;

namespace Nexus.Core.Bootstrap
{
    /// <summary>Result of a bootstrap attempt; never throws so the shell can show an error screen.</summary>
    public sealed class BootResult
    {
        public bool Succeeded { get; private set; }

        public string Error { get; private set; }

        public ServiceRegistry Services { get; private set; }

        public static BootResult Success(ServiceRegistry services)
        {
            return new BootResult { Succeeded = true, Error = string.Empty, Services = services };
        }

        public static BootResult Failure(string error)
        {
            return new BootResult { Succeeded = false, Error = error, Services = null };
        }
    }

    /// <summary>
    /// The composition root. Every service is created exactly once, in dependency order,
    /// and registered against an abstraction. Scenes never create their own services.
    /// </summary>
    public static class ApplicationBootstrapper
    {
        /// <summary>Creates, registers and installs the full runtime service graph.</summary>
        public static BootResult Boot()
        {
            if (AppContext.IsReady)
            {
                GameLog.Warning("Application is already bootstrapped; reusing the installed services.");
                return BootResult.Success(AppContext.Services);
            }

            try
            {
                IEventBus eventBus = new EventBus();
                IConfigurationService configuration = ConfigurationService.LoadOrDefaults();
                PlatformInfo platform = PlatformInfo.Detect();
                PlatformConfiguration platformConfiguration = PlatformConfiguration.For(platform);

                SettingsService settings = new SettingsService(eventBus, platformConfiguration);
                ISaveService saveService = JsonFileSaveService.CreateDefault();
                SceneLoader sceneLoader = SceneLoader.Create(eventBus);

                SimulationManager simulation = new SimulationManager(eventBus);
                GameSessionService sessionService = new GameSessionService(eventBus, configuration, simulation);

                ServiceRegistry services = Compose(
                    eventBus,
                    configuration,
                    platform,
                    platformConfiguration,
                    settings,
                    saveService,
                    sceneLoader,
                    simulation,
                    sessionService);

                AppContext.Install(services);

                Application.targetFrameRate = configuration.Game.TargetFrameRateFor(platform.IsMobile);
                settings.ApplyAll();

                GameManager gameManager = services.Get<GameManager>();
                gameManager.Initialize();

                eventBus.Publish(new ApplicationInitializedEvent(configuration.Game.BuildVersion));

                GameLog.Info("Application bootstrapped for " + platform + " (" + platformConfiguration + ").");
                return BootResult.Success(services);
            }
            catch (Exception exception)
            {
                GameLog.Exception(exception, "Application bootstrap failed.");
                return BootResult.Failure(exception.Message);
            }
        }

        /// <summary>
        /// Builds the service graph without touching the AppContext or the scene manager.
        /// Used directly by tests with fake implementations.
        /// </summary>
        public static ServiceRegistry Compose(
            IEventBus eventBus,
            IConfigurationService configuration,
            IPlatformInfo platform,
            PlatformConfiguration platformConfiguration,
            ISettingsService settings,
            ISaveService saveService,
            ISceneLoader sceneLoader,
            ISimulationManager simulation,
            IGameSessionService sessionService = null)
        {
            if (eventBus == null)
            {
                throw new ArgumentNullException(nameof(eventBus));
            }

            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            if (sceneLoader == null)
            {
                throw new ArgumentNullException(nameof(sceneLoader));
            }

            ISaveService resolvedSaveService = saveService ?? new NullSaveService();
            ISimulationManager resolvedSimulation = simulation ?? new SimulationManager(eventBus);

            // Configuration is validated inside SimulationSettings, so an edited asset can
            // never produce an unusable clock.
            resolvedSimulation.Configure(SimulationSettings.FromConfiguration(configuration.Game));

            // Planet is the first domain system on the Phase 02 graph. It is registered here,
            // once, before any session starts; every later domain system follows the same rule.
            if (configuration.Game != null)
            {
                resolvedSimulation.Systems.Register(new PlanetSystem(configuration.Game.DefaultPlanet, eventBus));
            }

            ServiceRegistry services = new ServiceRegistry();
            services.Register(eventBus);
            services.Register(configuration);
            services.Register(platform ?? new PlatformInfo(PlatformKind.Unknown, 0f, 0f));
            services.Register(platformConfiguration ?? PlatformConfiguration.For(null));
            services.Register(resolvedSaveService);
            services.Register(settings ?? new SettingsService(eventBus, null));
            services.Register(sceneLoader);
            services.Register(resolvedSimulation);
            services.Register(sessionService ?? new GameSessionService(eventBus, configuration, resolvedSimulation));
            services.Register(new GameManager(eventBus, sceneLoader, configuration, resolvedSaveService));
            return services;
        }
    }
}

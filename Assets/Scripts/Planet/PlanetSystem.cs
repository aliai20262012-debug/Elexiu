using System.Collections.Generic;
using Nexus.Core.Events;
using Nexus.Simulation;

namespace Nexus.Planet
{
    /// <summary>
    /// Planet domain system. It is a plain <see cref="ISimulationSystem"/> (no MonoBehaviour,
    /// no scene lookups): it builds the deterministic surface model from the session seed,
    /// advances the planet calendar on every deterministic tick and publishes a coarse
    /// environment update once per simulated day.
    ///
    /// The renderer builds its own view from the same definition and seed, so simulation and
    /// presentation stay independent while producing the same world.
    /// </summary>
    public sealed class PlanetSystem : SimulationSystemBase
    {
        public const string SystemIdentifier = "planet";
        public const int SystemOrder = 10;

        private readonly PlanetDefinition _definition;
        private readonly IEventBus _eventBus;

        private List<PlanetRegion> _regions;
        private PlanetSurface _surface;
        private PlanetSimulationState _state;
        private long _lastPublishedDay = -1;

        public PlanetSystem(PlanetDefinition definition, IEventBus eventBus = null)
            : base(SystemIdentifier, SystemOrder)
        {
            _definition = definition != null ? definition : PlanetDefinition.LoadDefault();
            _eventBus = eventBus;
        }

        public PlanetDefinition Definition
        {
            get { return _definition; }
        }

        /// <summary>Deterministic surface model; null before initialization.</summary>
        public PlanetSurface Surface
        {
            get { return _surface; }
        }

        /// <summary>Live calendar and per-region environment; null before initialization.</summary>
        public PlanetSimulationState State
        {
            get { return _state; }
        }

        public IReadOnlyList<PlanetRegion> Regions
        {
            get { return _regions; }
        }

        public bool HasState
        {
            get { return _state != null && _surface != null; }
        }

        /// <summary>Environment of a region at the current tick.</summary>
        public bool TryGetRegionEnvironment(int regionId, out PlanetRegionEnvironment environment)
        {
            if (_state == null)
            {
                environment = default(PlanetRegionEnvironment);
                return false;
            }

            return _state.TryGetEnvironment(regionId, out environment);
        }

        protected override void OnInitialize(SimulationInitializationContext context)
        {
            IReadOnlyList<PlanetRegion> definitionRegions = _definition.Regions;
            _regions = new List<PlanetRegion>(definitionRegions.Count);
            for (int i = 0; i < definitionRegions.Count; i++)
            {
                if (definitionRegions[i] != null)
                {
                    _regions.Add(definitionRegions[i]);
                }
            }

            _surface = new PlanetSurface(PlanetSurfaceSettings.FromDefinition(_definition, context.Seed), _regions);
            _state = new PlanetSimulationState(_definition, _regions);
            _state.Sync(0.0);
            _lastPublishedDay = -1;
        }

        protected override void OnTick(SimulationContext context)
        {
            if (_state == null)
            {
                return;
            }

            _state.Sync(context.ElapsedSeconds);
            PublishEnvironmentIfDayChanged();
        }

        protected override void OnShutdown()
        {
            _surface = null;
            _regions = null;
            _state = null;
            _lastPublishedDay = -1;
        }

        private void PublishEnvironmentIfDayChanged()
        {
            if (_eventBus == null || _state == null)
            {
                return;
            }

            long day = _state.DayNumber;
            if (day == _lastPublishedDay)
            {
                return;
            }

            _lastPublishedDay = day;
            _eventBus.Publish(new PlanetEnvironmentUpdatedEvent(
                _state.PlanetDays,
                day,
                _state.SeasonPhase,
                _state.MeanTemperatureC,
                _regions != null ? _regions.Count : 0));
        }
    }
}

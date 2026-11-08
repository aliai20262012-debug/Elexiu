using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nexus.Planet
{
    /// <summary>
    /// Live, per-session state of the planet: the simulated calendar (days, season phase) and
    /// the derived environment of every region. It is a pure function of the simulation time
    /// handed in by the clock, so replaying the same ticks always reproduces the same values.
    ///
    /// It contains no Unity objects and never touches the scene; the presentation layer reads
    /// it through events or through <see cref="PlanetSystem"/>.
    /// </summary>
    public sealed class PlanetSimulationState
    {
        private readonly PlanetDefinition _definition;
        private readonly IReadOnlyList<PlanetRegion> _regions;
        private double _elapsedSeconds;

        public PlanetSimulationState(PlanetDefinition definition, IReadOnlyList<PlanetRegion> regions)
        {
            _definition = definition != null ? definition : PlanetDefinition.CreateRuntimeDefault();
            _regions = regions ?? _definition.Regions;
            _elapsedSeconds = 0.0;
        }

        public PlanetDefinition Definition
        {
            get { return _definition; }
        }

        public IReadOnlyList<PlanetRegion> Regions
        {
            get { return _regions; }
        }

        public double ElapsedSeconds
        {
            get { return _elapsedSeconds; }
        }

        /// <summary>Simulated days since the session started (fractional).</summary>
        public double PlanetDays
        {
            get { return _elapsedSeconds / Math.Max(1.0, _definition.DayLengthSeconds); }
        }

        /// <summary>Whole simulated day, starting at 0.</summary>
        public long DayNumber
        {
            get { return (long)Math.Floor(PlanetDays); }
        }

        /// <summary>Position in the orbital cycle: 0 = northern summer, 0.5 = northern winter.</summary>
        public float SeasonPhase
        {
            get
            {
                double years = PlanetDays / Math.Max(1.0, _definition.OrbitalPeriodDays);
                double phase = years - Math.Floor(years);
                return (float)phase;
            }
        }

        /// <summary>Player-facing season label derived from <see cref="SeasonPhase"/>.</summary>
        public string SeasonLabel
        {
            get { return PlanetLabels.Season(SeasonPhase); }
        }

        /// <summary>Mean temperature across every region at the current season.</summary>
        public float MeanTemperatureC
        {
            get
            {
                if (_regions == null || _regions.Count == 0)
                {
                    return 0f;
                }

                float sum = 0f;
                for (int i = 0; i < _regions.Count; i++)
                {
                    sum += TemperatureOf(_regions[i]);
                }

                return sum / _regions.Count;
            }
        }

        /// <summary>Deterministically re-derives the state from the simulation clock.</summary>
        public void Sync(double elapsedSeconds)
        {
            _elapsedSeconds = elapsedSeconds < 0.0 ? 0.0 : elapsedSeconds;
        }

        public void Reset()
        {
            _elapsedSeconds = 0.0;
        }

        /// <summary>
        /// Temperature for a region: the authored climate envelope evaluated at the current
        /// season in the correct hemisphere, cooled by latitude.
        /// </summary>
        public float TemperatureOf(PlanetRegion region)
        {
            return TemperatureFor(region, SeasonPhase);
        }

        /// <summary>
        /// Region temperature at an arbitrary season phase. Shared by the simulation system and
        /// the presentation layer so both always show the same number.
        /// </summary>
        public static float TemperatureFor(PlanetRegion region, float seasonPhase)
        {
            if (region == null)
            {
                return 0f;
            }

            float hemisphereOffset = region.LatitudeDegrees < 0f ? 0.5f : 0f;
            float latitudeCooling = (Mathf.Abs(region.LatitudeDegrees) / 90f) * 10f;
            return region.Climate.TemperatureAt(seasonPhase + hemisphereOffset) - latitudeCooling;
        }

        /// <summary>Environment of one region at the current simulation time.</summary>
        public PlanetRegionEnvironment EnvironmentOf(PlanetRegion region)
        {
            if (region == null)
            {
                return new PlanetRegionEnvironment(RegionLocator.NoRegion, 0f, 0f, 0f, false);
            }

            bool daylight = IsDaylightAt(region);
            return new PlanetRegionEnvironment(
                region.Id,
                TemperatureOf(region),
                region.Climate.Humidity,
                region.Habitability.Score,
                daylight);
        }

        public bool TryGetEnvironment(int regionId, out PlanetRegionEnvironment environment)
        {
            environment = default(PlanetRegionEnvironment);
            if (_regions == null)
            {
                return false;
            }

            for (int i = 0; i < _regions.Count; i++)
            {
                PlanetRegion region = _regions[i];
                if (region == null || region.Id != regionId)
                {
                    continue;
                }

                environment = EnvironmentOf(region);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Simplified day/night test: the planet spins once per <see cref="PlanetDefinition.DayLengthSeconds"/>
        /// and the sun is fixed, so the lit hemisphere is the one facing +Z of the planet.
        /// </summary>
        public bool IsDaylightAt(PlanetRegion region)
        {
            if (region == null)
            {
                return false;
            }

            double rotation = (PlanetDays - Math.Floor(PlanetDays)) * Math.PI * 2.0;
            float sunLongitude = -(float)(rotation);
            float difference = Mathf.DeltaAngle(region.LongitudeDegrees, sunLongitude);
            return Mathf.Abs(difference) <= 90f;
        }
    }
}

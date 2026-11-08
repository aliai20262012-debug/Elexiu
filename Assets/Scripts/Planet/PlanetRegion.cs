using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nexus.Planet
{
    /// <summary>
    /// Immutable region definition: identity, terrain and water classification, climate,
    /// resources, environmental indicators and habitability. Regions are authored data; the
    /// live, per-tick environment lives in <see cref="PlanetSimulationState"/> so simulation
    /// values never leak back into the authored asset.
    /// </summary>
    public sealed class PlanetRegion
    {
        private readonly ResourceDeposit[] _resources;

        public PlanetRegion(
            int id,
            string code,
            string name,
            string description,
            RegionTerrainCategory terrain,
            WaterClassification water,
            float landFraction,
            float latitudeDegrees,
            float longitudeDegrees,
            float angularRadiusDegrees,
            ClimateProfile climate,
            IReadOnlyList<ResourceDeposit> resources,
            EnvironmentalIndicators environment,
            HabitabilityProfile habitability)
        {
            Id = id;
            Code = (code ?? string.Empty).Trim();
            Name = (name ?? string.Empty).Trim();
            Description = (description ?? string.Empty).Trim();
            Terrain = terrain;
            Water = water;
            LandFraction = Mathf.Clamp(landFraction, 0f, 1f);
            LatitudeDegrees = Mathf.Clamp(latitudeDegrees, PlanetGeo.MinLatitude, PlanetGeo.MaxLatitude);
            LongitudeDegrees = PlanetGeo.WrapLongitude(longitudeDegrees);
            AngularRadiusDegrees = Mathf.Clamp(angularRadiusDegrees, 1f, 89f);
            Climate = climate ?? new ClimateProfile(0f, 0f, 0f, 0f, 0f);
            Environment = environment ?? EnvironmentalIndicators.Neutral;
            Habitability = habitability ?? new HabitabilityProfile(0f, "UNKNOWN");
            Direction = PlanetGeo.DirectionFromLatLon(LatitudeDegrees, LongitudeDegrees);

            if (resources == null)
            {
                _resources = new ResourceDeposit[0];
            }
            else
            {
                _resources = new ResourceDeposit[resources.Count];
                for (int i = 0; i < resources.Count; i++)
                {
                    _resources[i] = resources[i];
                }
            }
        }

        public int Id { get; }

        /// <summary>Short player-facing code, for example <c>NX-AU-01</c>.</summary>
        public string Code { get; }

        public string Name { get; }

        /// <summary>One or two sentences of flavour text; never used by simulation logic.</summary>
        public string Description { get; }

        public RegionTerrainCategory Terrain { get; }

        public WaterClassification Water { get; }

        /// <summary>Share of the region covered by land, 0..1.</summary>
        public float LandFraction { get; }

        public float LatitudeDegrees { get; }

        public float LongitudeDegrees { get; }

        public float AngularRadiusDegrees { get; }

        public ClimateProfile Climate { get; }

        public EnvironmentalIndicators Environment { get; }

        public HabitabilityProfile Habitability { get; }

        /// <summary>Unit direction of the region centre, precomputed from latitude/longitude.</summary>
        public Vector3 Direction { get; }

        public IReadOnlyList<ResourceDeposit> Resources
        {
            get { return _resources; }
        }

        /// <summary>True for regions whose surface is mostly water (open ocean regions).</summary>
        public bool IsWaterRegion
        {
            get { return Water == WaterClassification.OpenOcean || LandFraction < 0.2f; }
        }

        /// <summary>Terrain label shown by the region panel, e.g. "PLAINS".</summary>
        public string TerrainLabel
        {
            get { return PlanetLabels.Terrain(Terrain); }
        }

        public string WaterLabel
        {
            get { return PlanetLabels.Water(Water); }
        }

        /// <summary>Highest abundance deposit; returns false for resource-less regions.</summary>
        public bool TryGetDominantResource(out ResourceDeposit deposit)
        {
            deposit = default(ResourceDeposit);
            if (_resources.Length == 0)
            {
                return false;
            }

            int best = 0;
            for (int i = 1; i < _resources.Length; i++)
            {
                if (_resources[i].Abundance > _resources[best].Abundance)
                {
                    best = i;
                }
            }

            deposit = _resources[best];
            return true;
        }

        /// <summary>Abundance of a resource family, or 0 when the region has none.</summary>
        public float AbundanceOf(ResourceType type)
        {
            for (int i = 0; i < _resources.Length; i++)
            {
                if (_resources[i].Type == type)
                {
                    return _resources[i].Abundance;
                }
            }

            return 0f;
        }

        public override string ToString()
        {
            return Code + " " + Name + " (" + Terrain + ")";
        }
    }
}

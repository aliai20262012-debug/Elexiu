using System.Collections.Generic;
using NUnit.Framework;
using Nexus.Planet;
using UnityEngine;

namespace Nexus.Tests.EditMode
{
    /// <summary>
    /// Heightfield and palette checks. The surface is the single source of truth shared by the
    /// renderer, the simulation system and picking, so determinism is asserted here.
    /// </summary>
    public class PlanetSurfaceTests
    {
        private const int SampleCount = 128;

        private static PlanetSurface CreateSurface(int seed)
        {
            PlanetSurfaceSettings settings = new PlanetSurfaceSettings(seed, 0.5f, 1.35f, 7.5f, 0.55f, 0.03f, 0.012f);
            return new PlanetSurface(settings, PlanetRegionLibrary.CreateDefaultRegions());
        }

        private static List<Vector3> SampleDirections()
        {
            List<Vector3> directions = new List<Vector3>(SampleCount);
            for (int i = 0; i < SampleCount; i++)
            {
                directions.Add(PlanetGeo.FibonacciDirection(i, SampleCount));
            }

            return directions;
        }

        [Test]
        public void SameSeed_ProducesIdenticalHeightField()
        {
            PlanetSurface first = CreateSurface(4242);
            PlanetSurface second = CreateSurface(4242);
            List<Vector3> directions = SampleDirections();

            for (int i = 0; i < directions.Count; i++)
            {
                Assert.AreEqual(first.HeightAt(directions[i]), second.HeightAt(directions[i]), 0.000001f);
            }
        }

        [Test]
        public void DifferentSeed_ChangesTheSurface()
        {
            PlanetSurface first = CreateSurface(1);
            PlanetSurface second = CreateSurface(2);
            List<Vector3> directions = SampleDirections();

            int differences = 0;
            for (int i = 0; i < directions.Count; i++)
            {
                if (Mathf.Abs(first.HeightAt(directions[i]) - second.HeightAt(directions[i])) > 0.01f)
                {
                    differences++;
                }
            }

            Assert.Greater(differences, 0, "A different seed must generate a different planet.");
        }

        [Test]
        public void Height_IsAlwaysNormalised()
        {
            PlanetSurface surface = CreateSurface(99);
            List<Vector3> directions = SampleDirections();

            for (int i = 0; i < directions.Count; i++)
            {
                float height = surface.HeightAt(directions[i]);
                Assert.That(height, Is.InRange(0f, 1f));
            }

            // Degenerate input must not throw or produce NaN.
            Assert.That(surface.HeightAt(Vector3.zero), Is.InRange(0f, 1f));
        }

        [Test]
        public void Surface_ContainsBothLandAndOcean()
        {
            PlanetSurface surface = CreateSurface(2026);
            List<Vector3> directions = SampleDirections();

            int land = 0;
            for (int i = 0; i < directions.Count; i++)
            {
                if (surface.IsLand(directions[i]))
                {
                    land++;
                }
            }

            float ratio = land / (float)directions.Count;
            Assert.That(ratio, Is.InRange(0.05f, 0.95f), "A useful planet needs both landmasses and oceans.");
        }

        [Test]
        public void RegionLookup_MatchesTheLocator()
        {
            PlanetSurface surface = CreateSurface(7);
            List<Vector3> directions = SampleDirections();

            for (int i = 0; i < directions.Count; i++)
            {
                PlanetRegion direct = surface.RegionAt(directions[i]);
                int expected = RegionLocator.FindRegionId(surface.Regions, directions[i]);

                if (direct == null)
                {
                    Assert.AreEqual(RegionLocator.NoRegion, expected);
                }
                else
                {
                    Assert.AreEqual(direct.Id, expected);
                }
            }
        }

        [Test]
        public void NearestRegion_AlwaysResolvesForValidDirections()
        {
            PlanetSurface surface = CreateSurface(11);
            List<Vector3> directions = SampleDirections();

            for (int i = 0; i < directions.Count; i++)
            {
                Assert.IsNotNull(surface.NearestRegionAt(directions[i]));
            }
        }

        [Test]
        public void ElevationAndDepth_AreNormalised()
        {
            PlanetSurface surface = CreateSurface(303);
            List<Vector3> directions = SampleDirections();

            for (int i = 0; i < directions.Count; i++)
            {
                Assert.That(surface.LandElevation(directions[i]), Is.InRange(0f, 1f));
                Assert.That(surface.WaterDepth(directions[i]), Is.InRange(0f, 1f));
            }
        }

        [Test]
        public void Albedo_IsOpaqueAndInRange()
        {
            PlanetSurface surface = CreateSurface(808);
            List<Vector3> directions = SampleDirections();

            for (int i = 0; i < directions.Count; i++)
            {
                Color color = surface.AlbedoAt(directions[i]);
                Assert.AreEqual(1f, color.a, 0.0001f);
                Assert.That(color.r, Is.InRange(0f, 1f));
                Assert.That(color.g, Is.InRange(0f, 1f));
                Assert.That(color.b, Is.InRange(0f, 1f));
            }
        }

        [Test]
        public void OceanIsDarkerThanShallowWater()
        {
            Color deep = PlanetSurface.TerrainColor(RegionTerrainCategory.Ocean);
            Color land = PlanetSurface.TerrainColor(RegionTerrainCategory.Plains);

            Assert.Less(deep.grayscale, land.grayscale, "Oceans must read darker than land at a glance.");
        }

        [Test]
        public void AccentColours_AreStableAndDistinct()
        {
            IReadOnlyList<PlanetRegion> regions = PlanetRegionLibrary.CreateDefaultRegions();

            Color first = PlanetSurface.AccentFor(regions[0]);
            Color again = PlanetSurface.AccentFor(regions[0]);

            Assert.AreEqual(first, again);
            Assert.AreNotEqual(first, PlanetSurface.AccentFor(regions[1]), "Regions should not share an accent.");
        }

        [Test]
        public void SurfaceSettings_FromDefinition_UsesSessionSeed()
        {
            PlanetDefinition definition = PlanetDefinition.CreateRuntimeDefault();
            try
            {
                PlanetSurfaceSettings a = PlanetSurfaceSettings.FromDefinition(definition, 10);
                PlanetSurfaceSettings b = PlanetSurfaceSettings.FromDefinition(definition, 20);

                Assert.AreNotEqual(a.Seed, b.Seed);
                Assert.AreEqual(definition.SeaLevel, a.SeaLevel, 0.0001f);
                Assert.AreEqual(definition.SurfaceRelief, a.Relief, 0.0001f);
            }
            finally
            {
                Object.DestroyImmediate(definition);
            }
        }
    }
}

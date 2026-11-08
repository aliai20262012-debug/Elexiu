using System.Collections.Generic;
using NUnit.Framework;
using Nexus.Planet;
using UnityEngine;

namespace Nexus.Tests.EditMode
{
    /// <summary>
    /// Spherical geometry and region identification. These are the functions the picking ray
    /// and the surface tint both depend on, so they are tested in isolation.
    /// </summary>
    public class RegionLocatorTests
    {
        [Test]
        public void DirectionFromLatLon_RoundTrips()
        {
            Vector3 direction = PlanetGeo.DirectionFromLatLon(34f, -18f);

            float latitude;
            float longitude;
            PlanetGeo.LatLonFromDirection(direction, out latitude, out longitude);

            Assert.AreEqual(34f, latitude, 0.001f);
            Assert.AreEqual(-18f, longitude, 0.001f);
            Assert.AreEqual(1f, direction.magnitude, 0.0001f);
        }

        [Test]
        public void DirectionFromLatLon_HandlesPoles()
        {
            Vector3 north = PlanetGeo.DirectionFromLatLon(90f, 0f);
            Vector3 south = PlanetGeo.DirectionFromLatLon(-90f, 0f);

            Assert.AreEqual(1f, north.y, 0.0001f);
            Assert.AreEqual(-1f, south.y, 0.0001f);
        }

        [Test]
        public void AngularDistance_KnownValues()
        {
            Assert.AreEqual(0f, PlanetGeo.AngularDistanceDegrees(Vector3.forward, Vector3.forward), 0.001f);
            Assert.AreEqual(90f, PlanetGeo.AngularDistanceDegrees(Vector3.forward, Vector3.right), 0.001f);
            Assert.AreEqual(180f, PlanetGeo.AngularDistanceDegrees(Vector3.forward, Vector3.back), 0.001f);
        }

        [Test]
        public void WrapLongitude_NormalisesOutOfRangeValues()
        {
            Assert.AreEqual(-170f, PlanetGeo.WrapLongitude(190f), 0.001f);
            Assert.AreEqual(170f, PlanetGeo.WrapLongitude(-190f), 0.001f);
            Assert.AreEqual(0f, PlanetGeo.WrapLongitude(360f), 0.001f);
            Assert.AreEqual(0f, PlanetGeo.WrapLongitude(float.NaN), 0.001f);
        }

        [Test]
        public void TryFindRegion_ReturnsRegionAtItsCentre()
        {
            List<PlanetRegion> regions = CreateRegions();

            PlanetRegion region;
            bool found = RegionLocator.TryFindRegion(regions, regions[1].Direction, out region);

            Assert.IsTrue(found);
            Assert.AreEqual(regions[1].Id, region.Id);
        }

        [Test]
        public void TryFindRegion_ReturnsFalseOutsideEveryRadius()
        {
            List<PlanetRegion> regions = new List<PlanetRegion>
            {
                MakeRegion(1, 0f, 0f, 5f)
            };

            PlanetRegion region;
            bool found = RegionLocator.TryFindRegion(regions, PlanetGeo.DirectionFromLatLon(0f, 180f), out region);

            Assert.IsFalse(found);
            Assert.IsNull(region);
            Assert.AreEqual(RegionLocator.NoRegion, RegionLocator.FindRegionId(regions, PlanetGeo.DirectionFromLatLon(0f, 180f)));
        }

        [Test]
        public void TryFindRegion_PrefersSmallerNormalisedDistance()
        {
            List<PlanetRegion> regions = new List<PlanetRegion>
            {
                MakeRegion(1, 0f, 0f, 40f),
                MakeRegion(2, 0f, 2f, 5f)
            };

            PlanetRegion region;
            bool found = RegionLocator.TryFindRegion(regions, PlanetGeo.DirectionFromLatLon(0f, 1f), out region);

            Assert.IsTrue(found);
            Assert.AreEqual(2, region.Id, "The small region nested inside the large one must win.");
        }

        [Test]
        public void FindNearestRegionIndex_IgnoresRadii()
        {
            List<PlanetRegion> regions = CreateRegions();
            Vector3 direction = PlanetGeo.DirectionFromLatLon(-70f, 170f);

            int index = RegionLocator.FindNearestRegionIndex(regions, direction);

            Assert.GreaterOrEqual(index, 0);
            Assert.Less(index, regions.Count);
        }

        [Test]
        public void TryFindRegionById_FindsKnownAndRejectsUnknown()
        {
            List<PlanetRegion> regions = CreateRegions();

            PlanetRegion found;
            Assert.IsTrue(RegionLocator.TryFindRegionById(regions, 2, out found));
            Assert.AreEqual(2, found.Id);

            PlanetRegion missing;
            Assert.IsFalse(RegionLocator.TryFindRegionById(regions, 999, out missing));
            Assert.IsNull(missing);
        }

        [Test]
        public void IsUsableDirection_RejectsDegenerateInput()
        {
            Assert.IsFalse(PlanetGeo.IsUsableDirection(Vector3.zero));
            Assert.IsFalse(PlanetGeo.IsUsableDirection(new Vector3(float.NaN, 0f, 1f)));
            Assert.IsTrue(PlanetGeo.IsUsableDirection(Vector3.up));
        }

        [Test]
        public void FibonacciDirection_IsDeterministicAndNormalised()
        {
            Vector3 first = PlanetGeo.FibonacciDirection(3, 12);
            Vector3 second = PlanetGeo.FibonacciDirection(3, 12);

            Assert.AreEqual(first.x, second.x, 0.000001f);
            Assert.AreEqual(first.y, second.y, 0.000001f);
            Assert.AreEqual(1f, first.magnitude, 0.001f);
        }

        private static List<PlanetRegion> CreateRegions()
        {
            return new List<PlanetRegion>
            {
                MakeRegion(1, 30f, 40f, 14f),
                MakeRegion(2, -12f, 100f, 12f),
                MakeRegion(3, 60f, -140f, 16f)
            };
        }

        private static PlanetRegion MakeRegion(int id, float latitude, float longitude, float radius)
        {
            return new PlanetRegion(
                id,
                "NX-" + id.ToString("00"),
                "REGION " + id,
                "test",
                RegionTerrainCategory.Plains,
                WaterClassification.Coastal,
                0.7f,
                latitude,
                longitude,
                radius,
                new ClimateProfile(12f, 10f, 0.5f, 12f, 400f),
                new[] { new ResourceDeposit(ResourceType.Minerals, 0.4f) },
                EnvironmentalIndicators.Neutral,
                new HabitabilityProfile(0.5f, "TEST"));
        }
    }
}

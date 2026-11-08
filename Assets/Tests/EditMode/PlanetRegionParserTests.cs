using System.Collections.Generic;
using NUnit.Framework;
using Nexus.Planet;
using UnityEngine;

namespace Nexus.Tests.EditMode
{
    /// <summary>
    /// Parser robustness: the region file is designer editable data, so every malformed entry
    /// must degrade to a logged issue instead of breaking the session.
    /// </summary>
    public class PlanetRegionParserTests
    {
        private const string ShippedRegionResourcePath = "Config/Planet/nexus-01.regions";

        private List<string> _issues;

        [SetUp]
        public void SetUp()
        {
            _issues = new List<string>();
        }

        private IReadOnlyList<PlanetRegion> Parse(string json)
        {
            return PlanetRegionParser.Parse(json, "NEXUS-01", _issues);
        }

        [Test]
        public void Parse_ReadsEveryField()
        {
            const string json = @"{
              ""planetId"": ""NEXUS-01"",
              ""regions"": [{
                ""id"": 4,
                ""code"": ""NX-TT-04"",
                ""name"": ""TEST BASIN"",
                ""description"": ""A test region."",
                ""terrain"": ""Highlands"",
                ""water"": ""OceanAccess"",
                ""landFraction"": 0.7,
                ""latitude"": 12.5,
                ""longitude"": -40.0,
                ""radius"": 14.0,
                ""climate"": { ""meanTemperatureC"": 21.5, ""temperatureRangeC"": 9.0, ""humidity"": 0.55, ""windKph"": 17.0, ""precipitationMm"": 640.0 },
                ""resources"": [ { ""type"": ""Metals"", ""abundance"": 0.6 }, { ""type"": ""Water"", ""abundance"": 0.4 } ],
                ""environment"": { ""airQuality"": 0.8, ""radiation"": 0.2, ""biodiversity"": 0.5, ""tectonicActivity"": 0.3 },
                ""habitability"": { ""score"": 0.66, ""limitingFactor"": ""WIND"" }
              }]
            }";

            IReadOnlyList<PlanetRegion> regions = Parse(json);

            Assert.AreEqual(1, regions.Count);
            Assert.AreEqual(0, _issues.Count, string.Join("; ", _issues));

            PlanetRegion region = regions[0];
            Assert.AreEqual(4, region.Id);
            Assert.AreEqual("NX-TT-04", region.Code);
            Assert.AreEqual("TEST BASIN", region.Name);
            Assert.AreEqual(RegionTerrainCategory.Highlands, region.Terrain);
            Assert.AreEqual(WaterClassification.OceanAccess, region.Water);
            Assert.AreEqual(0.7f, region.LandFraction, 0.0001f);
            Assert.AreEqual(12.5f, region.LatitudeDegrees, 0.0001f);
            Assert.AreEqual(-40f, region.LongitudeDegrees, 0.0001f);
            Assert.AreEqual(14f, region.AngularRadiusDegrees, 0.0001f);
            Assert.AreEqual(21.5f, region.Climate.MeanTemperatureC, 0.0001f);
            Assert.AreEqual(0.55f, region.Climate.Humidity, 0.0001f);
            Assert.AreEqual(2, region.Resources.Count);
            Assert.AreEqual(0.2f, region.Environment.Radiation, 0.0001f);
            Assert.AreEqual(0.66f, region.Habitability.Score, 0.0001f);
            Assert.AreEqual("WIND", region.Habitability.LimitingFactor);
        }

        [Test]
        public void Parse_RejectsInvalidJson()
        {
            IReadOnlyList<PlanetRegion> regions = Parse("this is not json");

            Assert.AreEqual(0, regions.Count);
            Assert.Greater(_issues.Count, 0);
        }

        [Test]
        public void Parse_RejectsEmptyDocument()
        {
            IReadOnlyList<PlanetRegion> regions = Parse(string.Empty);

            Assert.AreEqual(0, regions.Count);
            Assert.Greater(_issues.Count, 0);
        }

        [Test]
        public void Parse_SkipsDuplicateIds()
        {
            const string json = @"{ ""planetId"": ""NEXUS-01"", ""regions"": [
                { ""id"": 1, ""code"": ""NX-AA"", ""name"": ""A"", ""terrain"": ""Plains"", ""water"": ""Coastal"", ""landFraction"": 0.7, ""latitude"": 10, ""longitude"": 10, ""radius"": 12 },
                { ""id"": 1, ""code"": ""NX-BB"", ""name"": ""B"", ""terrain"": ""Plains"", ""water"": ""Coastal"", ""landFraction"": 0.7, ""latitude"": 20, ""longitude"": 20, ""radius"": 12 }
            ] }";

            IReadOnlyList<PlanetRegion> regions = Parse(json);

            Assert.AreEqual(1, regions.Count);
            StringAssert.Contains("duplicate id", string.Join(" ", _issues));
        }

        [Test]
        public void Parse_RejectsOutOfRangeLatitude()
        {
            const string json = @"{ ""regions"": [
                { ""id"": 1, ""code"": ""NX-AA"", ""name"": ""A"", ""terrain"": ""Plains"", ""water"": ""Coastal"", ""landFraction"": 0.7, ""latitude"": 120, ""longitude"": 10, ""radius"": 12 }
            ] }";

            IReadOnlyList<PlanetRegion> regions = Parse(json);

            Assert.AreEqual(0, regions.Count);
            StringAssert.Contains("latitude", string.Join(" ", _issues));
        }

        [Test]
        public void Parse_ClampsAngularRadius()
        {
            const string json = @"{ ""regions"": [
                { ""id"": 1, ""code"": ""NX-AA"", ""name"": ""A"", ""terrain"": ""Plains"", ""water"": ""Coastal"", ""landFraction"": 0.7, ""latitude"": 10, ""longitude"": 10, ""radius"": 120 }
            ] }";

            IReadOnlyList<PlanetRegion> regions = Parse(json);

            Assert.AreEqual(1, regions.Count);
            Assert.AreEqual(PlanetDataValidator.MaxAngularRadiusDegrees, regions[0].AngularRadiusDegrees, 0.0001f);
            StringAssert.Contains("clamped", string.Join(" ", _issues));
        }

        [Test]
        public void Parse_ClampsHumidityAndLandFraction()
        {
            const string json = @"{ ""regions"": [
                { ""id"": 1, ""code"": ""NX-AA"", ""name"": ""A"", ""terrain"": ""Plains"", ""water"": ""Coastal"", ""landFraction"": 4.0, ""latitude"": 10, ""longitude"": 10, ""radius"": 12,
                  ""climate"": { ""meanTemperatureC"": 10, ""temperatureRangeC"": 5, ""humidity"": 9.0, ""windKph"": 10, ""precipitationMm"": 100 } }
            ] }";

            IReadOnlyList<PlanetRegion> regions = Parse(json);

            Assert.AreEqual(1, regions.Count);
            Assert.AreEqual(1f, regions[0].LandFraction, 0.0001f);
            Assert.AreEqual(1f, regions[0].Climate.Humidity, 0.0001f);
            StringAssert.Contains("clamped", string.Join(" ", _issues));
        }

        [Test]
        public void Parse_UnknownTerrainFallsBackToPlains()
        {
            const string json = @"{ ""regions"": [
                { ""id"": 1, ""code"": ""NX-AA"", ""name"": ""A"", ""terrain"": ""Swamp"", ""water"": ""Coastal"", ""landFraction"": 0.7, ""latitude"": 10, ""longitude"": 10, ""radius"": 12 }
            ] }";

            IReadOnlyList<PlanetRegion> regions = Parse(json);

            Assert.AreEqual(1, regions.Count);
            Assert.AreEqual(RegionTerrainCategory.Plains, regions[0].Terrain);
            StringAssert.Contains("unknown terrain", string.Join(" ", _issues));
        }

        [Test]
        public void Parse_SkipsUnknownAndDuplicateResources()
        {
            const string json = @"{ ""regions"": [
                { ""id"": 1, ""code"": ""NX-AA"", ""name"": ""A"", ""terrain"": ""Plains"", ""water"": ""Coastal"", ""landFraction"": 0.7, ""latitude"": 10, ""longitude"": 10, ""radius"": 12,
                  ""resources"": [ { ""type"": ""Metals"", ""abundance"": 0.5 }, { ""type"": ""Metals"", ""abundance"": 0.9 }, { ""type"": ""Unobtanium"", ""abundance"": 1.0 } ] }
            ] }";

            IReadOnlyList<PlanetRegion> regions = Parse(json);

            Assert.AreEqual(1, regions.Count);
            Assert.AreEqual(1, regions[0].Resources.Count);
            string issues = string.Join(" ", _issues);
            StringAssert.Contains("duplicate resource", issues);
            StringAssert.Contains("unknown resource", issues);
        }

        [Test]
        public void Parse_WarnsWhenPlanetIdDiffers()
        {
            const string json = @"{ ""planetId"": ""OTHER-02"", ""regions"": [
                { ""id"": 1, ""code"": ""NX-AA"", ""name"": ""A"", ""terrain"": ""Plains"", ""water"": ""Coastal"", ""landFraction"": 0.7, ""latitude"": 10, ""longitude"": 10, ""radius"": 12 }
            ] }";

            Parse(json);

            StringAssert.Contains("OTHER-02", string.Join(" ", _issues));
        }

        [Test]
        public void ShippedRegionFile_ParsesCleanly()
        {
            TextAsset asset = Resources.Load<TextAsset>(ShippedRegionResourcePath);
            Assert.IsNotNull(asset, "Resources/" + ShippedRegionResourcePath + " must exist.");

            IReadOnlyList<PlanetRegion> regions = Parse(asset.text);

            Assert.AreEqual(8, regions.Count);
            Assert.AreEqual(0, _issues.Count, string.Join("; ", _issues));

            HashSet<int> ids = new HashSet<int>();
            HashSet<string> codes = new HashSet<string>();
            for (int i = 0; i < regions.Count; i++)
            {
                Assert.IsTrue(ids.Add(regions[i].Id), "Duplicate region id " + regions[i].Id);
                Assert.IsTrue(codes.Add(regions[i].Code), "Duplicate region code " + regions[i].Code);
                Assert.Greater(regions[i].Resources.Count, 0, regions[i].Code + " should have resources.");
            }
        }
    }
}

using System.Collections.Generic;
using NUnit.Framework;
using Nexus.Planet;
using UnityEngine;

namespace Nexus.Tests.EditMode
{
    /// <summary>
    /// Identity, validation and determinism checks for <see cref="PlanetDefinition"/>.
    /// The shipped asset is loaded through Resources exactly like the game does.
    /// </summary>
    public class PlanetDefinitionTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _created.Count; i++)
            {
                if (_created[i] != null)
                {
                    Object.DestroyImmediate(_created[i]);
                }
            }

            _created.Clear();
        }

        private PlanetDefinition CreateRuntimeDefault()
        {
            PlanetDefinition definition = PlanetDefinition.CreateRuntimeDefault();
            _created.Add(definition);
            return definition;
        }

        [Test]
        public void RuntimeDefault_HasIdentityAndRegions()
        {
            PlanetDefinition definition = CreateRuntimeDefault();

            Assert.AreEqual("NEXUS-01", definition.PlanetId);
            Assert.IsFalse(string.IsNullOrEmpty(definition.DisplayName));
            Assert.GreaterOrEqual(definition.Regions.Count, 3);
            Assert.Greater(definition.RadiusKm, 0f);
            Assert.Greater(definition.DayLengthSeconds, 0f);
        }

        [Test]
        public void RuntimeDefault_PassesValidation()
        {
            PlanetValidationReport report = PlanetDataValidator.Validate(CreateRuntimeDefault());

            Assert.IsTrue(report.IsValid, report.Summary);
        }

        [Test]
        public void ShippedAsset_ExistsAndPassesValidation()
        {
            PlanetDefinition definition = Resources.Load<PlanetDefinition>(PlanetDefinition.DefaultResourcePath);
            Assert.IsNotNull(definition, "Resources/" + PlanetDefinition.DefaultResourcePath + " must exist.");
            _created.Add(definition);

            PlanetValidationReport report = PlanetDataValidator.Validate(definition);
            Assert.IsTrue(report.IsValid, report.Summary);
            Assert.AreEqual(8, definition.Regions.Count);
            Assert.AreEqual(0, definition.RegionIssues.Count, string.Join("; ", definition.RegionIssues));
        }

        [Test]
        public void ShippedAsset_UsesDataDrivenRegionFile()
        {
            PlanetDefinition definition = Resources.Load<PlanetDefinition>(PlanetDefinition.DefaultResourcePath);
            Assert.IsNotNull(definition);
            _created.Add(definition);

            Assert.IsNotNull(definition.RegionFile, "The shipped planet must reference its region JSON.");
            Assert.Greater(definition.RegionFile.text.Length, 0);
        }

        [Test]
        public void Validator_RejectsNullDefinition()
        {
            PlanetValidationReport report = PlanetDataValidator.Validate(null);

            Assert.IsFalse(report.IsValid);
        }

        [Test]
        public void Validator_RejectsTooFewRegions()
        {
            PlanetValidationReport report = PlanetDataValidator.ValidateRegions(new List<PlanetRegion>());

            Assert.IsFalse(report.IsValid);
            StringAssert.Contains("at least", string.Join(" ", report.Issues));
        }

        [Test]
        public void Validator_RejectsDuplicateIdsAndCodes()
        {
            List<PlanetRegion> regions = new List<PlanetRegion>
            {
                MakeRegion(1, "NX-AA", 10f, 20f),
                MakeRegion(1, "NX-AA", 30f, 40f),
                MakeRegion(2, "NX-BB", 50f, 60f)
            };

            PlanetValidationReport report = PlanetDataValidator.ValidateRegions(regions);

            Assert.IsFalse(report.IsValid);
            string summary = string.Join(" ", report.Issues);
            StringAssert.Contains("duplicate id", summary);
            StringAssert.Contains("duplicate code", summary);
        }

        [Test]
        public void Validator_RejectsWaterRegionWithLandTerrain()
        {
            PlanetRegion region = new PlanetRegion(
                9, "NX-SEA", "TEST SEA", "test",
                RegionTerrainCategory.Plains, WaterClassification.OpenOcean,
                0.05f, 0f, 0f, 12f,
                new ClimateProfile(10f, 10f, 0.5f, 10f, 100f),
                new ResourceDeposit[0],
                EnvironmentalIndicators.Neutral,
                new HabitabilityProfile(0.2f, "TEST"));

            PlanetValidationReport report = PlanetDataValidator.ValidateRegions(
                new List<PlanetRegion> { region, MakeRegion(1, "NX-AA", 10f, 20f), MakeRegion(2, "NX-BB", 30f, 40f) });

            Assert.IsFalse(report.IsValid);
            StringAssert.Contains("water region", string.Join(" ", report.Issues));
        }

        [Test]
        public void SurfaceSeed_DependsOnDefinitionAndSessionSeed()
        {
            PlanetDefinition definition = CreateRuntimeDefault();

            int a = definition.ResolveSurfaceSeed(100);
            int b = definition.ResolveSurfaceSeed(100);
            int c = definition.ResolveSurfaceSeed(101);

            Assert.AreEqual(a, b, "The same seeds must resolve identically.");
            Assert.AreNotEqual(a, c, "A different session seed must give a different planet.");
            Assert.AreNotEqual(0, a);
        }

        [Test]
        public void Properties_ClampUnusableValues()
        {
            PlanetDefinition definition = CreateRuntimeDefault();

            Assert.That(definition.MeshSubdivisions, Is.InRange(2, 6));
            Assert.That(definition.TextureResolution, Is.InRange(128, 2048));
            Assert.That(definition.SeaLevel, Is.InRange(0.05f, 0.95f));
            Assert.That(definition.SurfaceRelief, Is.InRange(0.002f, 0.15f));
            Assert.That(definition.AxialTiltDegrees, Is.InRange(0f, 90f));
        }

        private static PlanetRegion MakeRegion(int id, string code, float latitude, float longitude)
        {
            return new PlanetRegion(
                id, code, "REGION " + id, "test region",
                RegionTerrainCategory.Plains, WaterClassification.Coastal,
                0.7f, latitude, longitude, 12f,
                new ClimateProfile(12f, 10f, 0.5f, 12f, 400f),
                new[] { new ResourceDeposit(ResourceType.Minerals, 0.4f) },
                EnvironmentalIndicators.Neutral,
                new HabitabilityProfile(0.5f, "TEST"));
        }
    }
}

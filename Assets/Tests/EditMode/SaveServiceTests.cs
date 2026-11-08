using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Nexus.SaveSystem;

namespace Nexus.Tests.EditMode
{
    public class SaveServiceTests
    {
        private string _rootDirectory;
        private JsonFileSaveService _service;

        [SetUp]
        public void SetUp()
        {
            _rootDirectory = Path.Combine(Path.GetTempPath(), "nexus-save-tests-" + Guid.NewGuid().ToString("N"));
            _service = new JsonFileSaveService(_rootDirectory);
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                if (Directory.Exists(_rootDirectory))
                {
                    Directory.Delete(_rootDirectory, true);
                }
            }
            catch (IOException)
            {
                // A locked directory must never fail the test run.
            }
        }

        [Test]
        public void EmptyStore_HasNoSaves()
        {
            Assert.IsFalse(_service.HasAnySave);
            Assert.IsFalse(_service.Exists("slot-01"));
            Assert.AreEqual(0, _service.ListSlots().Count);
        }

        [Test]
        public void Save_ThenLoad_RoundTripsData()
        {
            SaveData data = SaveData.CreateNew("slot-01", "Terran Concord");
            data.Turn = 12;

            Assert.IsTrue(_service.Save(data));
            Assert.IsTrue(_service.HasAnySave);

            SaveData loaded;
            Assert.IsTrue(_service.TryLoad("slot-01", out loaded));
            Assert.IsNotNull(loaded);
            Assert.AreEqual("slot-01", loaded.SlotId);
            Assert.AreEqual("Terran Concord", loaded.CivilizationName);
            Assert.AreEqual(12, loaded.Turn);
            Assert.AreEqual(SaveData.CurrentSchemaVersion, loaded.SchemaVersion);
            Assert.IsFalse(string.IsNullOrEmpty(loaded.SavedAtUtc));
        }

        [Test]
        public void ListSlots_DescribesSavedSlot()
        {
            SaveData data = SaveData.CreateNew("campaign", "Nexus Directorate");
            data.Turn = 3;
            _service.Save(data);

            IReadOnlyList<SaveSlotInfo> slots = _service.ListSlots();

            Assert.AreEqual(1, slots.Count);
            Assert.AreEqual("campaign", slots[0].SlotId);
            Assert.AreEqual(3, slots[0].Turn);
            Assert.Greater(slots[0].SizeBytes, 0L);
        }

        [Test]
        public void Delete_RemovesSlot()
        {
            _service.Save(SaveData.CreateNew("slot-01", "Terran Concord"));

            Assert.IsTrue(_service.Delete("slot-01"));
            Assert.IsFalse(_service.Exists("slot-01"));
            Assert.IsFalse(_service.Delete("slot-01"), "Deleting a missing slot reports false.");
        }

        [Test]
        public void Save_WithoutSlotId_IsRejected()
        {
            SaveData data = SaveData.CreateNew(string.Empty, "Terran Concord");
            Assert.IsFalse(_service.Save(data));
        }

        [Test]
        public void TryLoad_MissingSlot_ReturnsFalse()
        {
            SaveData loaded;
            Assert.IsFalse(_service.TryLoad("does-not-exist", out loaded));
            Assert.IsNull(loaded);
        }

        [Test]
        public void NullSaveService_DisablesPersistence()
        {
            NullSaveService service = new NullSaveService();

            Assert.IsFalse(service.HasAnySave);
            Assert.IsFalse(service.Save(SaveData.CreateNew("slot-01", "Terran Concord")));
            Assert.AreEqual(0, service.ListSlots().Count);
        }
    }
}

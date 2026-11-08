using System;

namespace Nexus.SaveSystem
{
    /// <summary>
    /// Serialisable payload of a save slot. Phase 01 only persists the metadata needed
    /// for the main menu; the simulation state will be added in a later phase without
    /// breaking existing files (<see cref="SchemaVersion"/> is the migration hook).
    /// </summary>
    [Serializable]
    public sealed class SaveData
    {
        public const int CurrentSchemaVersion = 1;

        public int SchemaVersion = CurrentSchemaVersion;
        public string SlotId = "slot-01";
        public string CivilizationName = "Unnamed Civilization";
        public int Turn;
        public string SavedAtUtc = string.Empty;

        public static SaveData CreateNew(string slotId, string civilizationName)
        {
            return new SaveData
            {
                SchemaVersion = CurrentSchemaVersion,
                SlotId = slotId,
                CivilizationName = civilizationName,
                Turn = 0,
                SavedAtUtc = DateTime.UtcNow.ToString("o")
            };
        }
    }
}

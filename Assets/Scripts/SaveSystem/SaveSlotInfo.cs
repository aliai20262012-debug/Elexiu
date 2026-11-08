namespace Nexus.SaveSystem
{
    /// <summary>Lightweight slot description used to populate the continue button.</summary>
    public readonly struct SaveSlotInfo
    {
        public readonly string SlotId;
        public readonly int Turn;
        public readonly string SavedAtUtc;
        public readonly long SizeBytes;

        public SaveSlotInfo(string slotId, int turn, string savedAtUtc, long sizeBytes)
        {
            SlotId = slotId;
            Turn = turn;
            SavedAtUtc = savedAtUtc;
            SizeBytes = sizeBytes;
        }

        public override string ToString()
        {
            return SlotId + " (turn " + Turn + ")";
        }
    }
}

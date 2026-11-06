using System.Collections.Generic;

namespace Nexus.SaveSystem
{
    /// <summary>
    /// No-op implementation used when saving is disabled (for example on a platform
    /// without writable storage). The main menu stays functional: Continue is disabled.
    /// </summary>
    public sealed class NullSaveService : ISaveService
    {
        public bool HasAnySave
        {
            get { return false; }
        }

        public bool Exists(string slotId)
        {
            return false;
        }

        public bool Save(SaveData data)
        {
            return false;
        }

        public bool TryLoad(string slotId, out SaveData data)
        {
            data = null;
            return false;
        }

        public bool Delete(string slotId)
        {
            return false;
        }

        public IReadOnlyList<SaveSlotInfo> ListSlots()
        {
            return new SaveSlotInfo[0];
        }
    }
}

using System.Collections.Generic;

namespace Nexus.SaveSystem
{
    /// <summary>
    /// Persistence boundary. Gameplay code depends on this interface only; the concrete
    /// implementation can be swapped for a cloud provider without touching the simulation.
    /// </summary>
    public interface ISaveService
    {
        bool HasAnySave { get; }

        bool Exists(string slotId);

        bool Save(SaveData data);

        bool TryLoad(string slotId, out SaveData data);

        bool Delete(string slotId);

        IReadOnlyList<SaveSlotInfo> ListSlots();
    }
}

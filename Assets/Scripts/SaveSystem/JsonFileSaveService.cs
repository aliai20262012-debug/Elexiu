using System;
using System.Collections.Generic;
using System.IO;
using Nexus.Core.Logging;
using UnityEngine;

namespace Nexus.SaveSystem
{
    /// <summary>
    /// Local JSON persistence under Application.persistentDataPath. Every IO operation is
    /// guarded so a corrupt or read-only store degrades into "no save available" instead of
    /// crashing the application shell.
    /// </summary>
    public sealed class JsonFileSaveService : ISaveService
    {
        private const string FileExtension = ".json";

        private readonly string _rootDirectory;

        public string RootDirectory
        {
            get { return _rootDirectory; }
        }

        public JsonFileSaveService(string rootDirectory)
        {
            _rootDirectory = string.IsNullOrEmpty(rootDirectory)
                ? Path.Combine(Application.persistentDataPath, "Saves")
                : rootDirectory;
        }

        public static JsonFileSaveService CreateDefault()
        {
            return new JsonFileSaveService(Path.Combine(Application.persistentDataPath, "Saves"));
        }

        public bool HasAnySave
        {
            get { return ListSlots().Count > 0; }
        }

        public bool Exists(string slotId)
        {
            if (string.IsNullOrEmpty(slotId))
            {
                return false;
            }

            try
            {
                return File.Exists(PathFor(slotId));
            }
            catch (Exception exception)
            {
                GameLog.Exception(exception, "Save slot lookup failed for '" + slotId + "'.");
                return false;
            }
        }

        public bool Save(SaveData data)
        {
            if (data == null || string.IsNullOrEmpty(data.SlotId))
            {
                GameLog.Warning("Save rejected: missing save data or slot id.");
                return false;
            }

            try
            {
                Directory.CreateDirectory(_rootDirectory);
                data.SchemaVersion = SaveData.CurrentSchemaVersion;
                data.SavedAtUtc = DateTime.UtcNow.ToString("o");
                File.WriteAllText(PathFor(data.SlotId), JsonUtility.ToJson(data, true));
                return true;
            }
            catch (Exception exception)
            {
                GameLog.Exception(exception, "Failed to write save slot '" + data.SlotId + "'.");
                return false;
            }
        }

        public bool TryLoad(string slotId, out SaveData data)
        {
            data = null;
            if (string.IsNullOrEmpty(slotId))
            {
                return false;
            }

            try
            {
                string path = PathFor(slotId);
                if (!File.Exists(path))
                {
                    return false;
                }

                data = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
                return data != null;
            }
            catch (Exception exception)
            {
                GameLog.Exception(exception, "Failed to read save slot '" + slotId + "'.");
                data = null;
                return false;
            }
        }

        public bool Delete(string slotId)
        {
            if (string.IsNullOrEmpty(slotId))
            {
                return false;
            }

            try
            {
                string path = PathFor(slotId);
                if (!File.Exists(path))
                {
                    return false;
                }

                File.Delete(path);
                return true;
            }
            catch (Exception exception)
            {
                GameLog.Exception(exception, "Failed to delete save slot '" + slotId + "'.");
                return false;
            }
        }

        public IReadOnlyList<SaveSlotInfo> ListSlots()
        {
            List<SaveSlotInfo> slots = new List<SaveSlotInfo>();

            try
            {
                if (!Directory.Exists(_rootDirectory))
                {
                    return slots;
                }

                string[] files = Directory.GetFiles(_rootDirectory, "*" + FileExtension);
                for (int i = 0; i < files.Length; i++)
                {
                    SaveData data;
                    try
                    {
                        data = JsonUtility.FromJson<SaveData>(File.ReadAllText(files[i]));
                    }
                    catch (Exception)
                    {
                        continue;
                    }

                    if (data == null)
                    {
                        continue;
                    }

                    long size = 0;
                    try
                    {
                        size = new FileInfo(files[i]).Length;
                    }
                    catch (Exception)
                    {
                        size = 0;
                    }

                    slots.Add(new SaveSlotInfo(
                        string.IsNullOrEmpty(data.SlotId) ? Path.GetFileNameWithoutExtension(files[i]) : data.SlotId,
                        data.Turn,
                        data.SavedAtUtc,
                        size));
                }
            }
            catch (Exception exception)
            {
                GameLog.Exception(exception, "Failed to enumerate save slots in '" + _rootDirectory + "'.");
            }

            return slots;
        }

        private string PathFor(string slotId)
        {
            return Path.Combine(_rootDirectory, SanitizeSlotId(slotId) + FileExtension);
        }

        private static string SanitizeSlotId(string slotId)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            char[] buffer = slotId.ToCharArray();
            for (int i = 0; i < buffer.Length; i++)
            {
                for (int j = 0; j < invalid.Length; j++)
                {
                    if (buffer[i] == invalid[j])
                    {
                        buffer[i] = '_';
                        break;
                    }
                }
            }

            return new string(buffer);
        }
    }
}

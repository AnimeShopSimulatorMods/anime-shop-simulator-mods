using System;
using System.Collections.Generic;
using System.IO;
using Il2CppProject.Code.Gameplay.Interactions.Shelfs;
using Il2CppProject.Code.Gameplay.Player.Products;
using MelonLoader;
using MelonLoader.Utils;

namespace SmartRestockEmployees
{
    // Shelves the player has told employees to leave alone.
    //
    // This is the one piece of state the mod keeps of its own, and it exists because nothing in the
    // game distinguishes "bare because the player wants it bare" from "bare because nothing has been
    // put there yet". Forgetting a shelf produces the second, and the store-wide switch may then
    // refill it, which made the button's promise false.
    //
    // Slots are keyed by PersistentGuid, so the list is safe to keep in one file: the ids are GUIDs
    // and cannot collide between save files. A stale id for a shelf that no longer exists is simply
    // never matched.
    internal static class ShelfLocks
    {
        private const string FileName = "locked-shelves.txt";

        private static readonly HashSet<string> Locked = new HashSet<string>(StringComparer.Ordinal);
        private static string _path;
        private static bool _loaded;

        public static void Load()
        {
            if (_loaded) return;
            _loaded = true;

            try
            {
                _path = Path.Combine(ResolveUserData(), "SmartRestockEmployees", FileName);
                if (!File.Exists(_path)) return;

                foreach (var line in File.ReadAllLines(_path))
                {
                    var id = line.Trim();
                    if (id.Length > 0 && !id.StartsWith("#")) Locked.Add(id);
                }
                MelonLogger.Msg($"{ModInfo.Name}: {Locked.Count} shelf slot(s) set to stay empty.");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[ShelfLocks] Could not read {FileName}: {ex.Message}");
            }
        }

        public static bool IsLocked(ProductPricePlace place)
        {
            if (Locked.Count == 0) return false;
            var key = Key(place);
            return key != null && Locked.Contains(key);
        }

        public static bool IsShelfLocked(ShelfProducts shelf)
        {
            if (Locked.Count == 0 || shelf == null) return false;
            try
            {
                var places = shelf.Places;
                if (places == null) return false;
                for (int i = 0; i < places.Length; i++)
                    if (places[i] != null && IsLocked(places[i])) return true;
            }
            catch
            {
                return false;
            }
            return false;
        }

        public static void Lock(ShelfProducts shelf) => Apply(shelf, true);

        public static void Unlock(ShelfProducts shelf) => Apply(shelf, false);

        private static void Apply(ShelfProducts shelf, bool locked)
        {
            if (shelf == null) return;

            bool changed = false;
            try
            {
                var places = shelf.Places;
                if (places == null) return;

                for (int i = 0; i < places.Length; i++)
                {
                    var key = Key(places[i]);
                    if (key == null) continue;
                    changed |= locked ? Locked.Add(key) : Locked.Remove(key);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[ShelfLocks] Could not update a shelf: {ex.Message}");
            }

            if (changed) Save();
        }

        private static string Key(ProductPricePlace place)
        {
            if (place == null) return null;
            try
            {
                // PersistentId is the game's own slot key where it exists; the GUID is the fallback
                // for shelves that were never set up as building places.
                var id = place.PersistentId;
                if (!string.IsNullOrEmpty(id)) return id;

                var guid = place.PersistentGuid;
                var text = guid.ToString();
                return string.IsNullOrEmpty(text) || text == Il2CppSystem.Guid.Empty.ToString() ? null : text;
            }
            catch
            {
                return null;
            }
        }

        private static void Save()
        {
            if (string.IsNullOrEmpty(_path)) return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path));

                var lines = new List<string>
                {
                    "# Shelf slots employees must leave empty, one id per line.",
                    "# Written by " + ModInfo.Name + ". Delete a line to let employees use that slot again.",
                };
                lines.AddRange(Locked);

                File.WriteAllLines(_path, lines);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[ShelfLocks] Could not write {FileName}: {ex.Message}");
            }
        }

        private static string ResolveUserData()
        {
            try
            {
                var dir = MelonEnvironment.UserDataDirectory;
                if (!string.IsNullOrEmpty(dir)) return dir;
            }
            catch
            {
                // Older MelonLoader layouts; the relative path below is next to the game executable.
            }
            return "UserData";
        }
    }
}

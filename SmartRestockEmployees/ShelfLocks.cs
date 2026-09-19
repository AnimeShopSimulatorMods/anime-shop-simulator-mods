using System;
using System.Collections.Generic;
using System.IO;
using Il2CppProject.Code.Gameplay.Interactions.Shelfs;
using Il2CppProject.Code.Gameplay.Player.Products;
using MelonLoader;
using MelonLoader.Utils;

namespace SmartRestockEmployees
{
    // Shelf slots the player has told employees to leave alone.
    //
    // Locks are per slot, not per shelf. One shelf holds several slots and they are independent: a
    // player can pack away the figurines in two of them, set those two aside, and leave the rest of
    // the shelf working.
    //
    // This is the only state the mod keeps of its own, and it exists because nothing in the game
    // separates "bare because the player wants it bare" from "bare because nothing has been put there
    // yet". Slots are keyed by the game's own persistent id, so one file is safe across save files
    // and a stale id is simply never matched.
    internal static class ShelfLocks
    {
        private const string FileName = "locked-shelves.txt";

        private static readonly HashSet<string> Locked = new HashSet<string>(StringComparer.Ordinal);
        private static string _path;
        private static bool _loaded;
        private static bool _warnedAboutKeys;

        public static int Count => Locked.Count;

        public static void Load()
        {
            if (_loaded) return;
            _loaded = true;

            try
            {
                _path = Path.Combine(ResolveUserData(), "SmartRestockEmployees", FileName);
                if (!File.Exists(_path))
                {
                    MelonLogger.Msg($"[ShelfLocks] No {FileName} yet; no slots are set to stay empty.");
                    return;
                }

                foreach (var line in File.ReadAllLines(_path))
                {
                    var id = line.Trim();
                    if (id.Length > 0 && !id.StartsWith("#")) Locked.Add(id);
                }
                MelonLogger.Msg($"[ShelfLocks] {Locked.Count} slot(s) set to stay empty, from {_path}");
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

        public static int LockedSlots(ShelfProducts shelf)
        {
            if (Locked.Count == 0) return 0;
            int count = 0;
            ForEachSlot(shelf, place =>
            {
                if (IsLocked(place)) count++;
            });
            return count;
        }

        // How many slots on this shelf hold nothing right now. "Empty" is per slot: the shelf as a
        // whole does not have to be bare.
        public static int EmptySlots(ShelfProducts shelf)
        {
            int count = 0;
            ForEachSlot(shelf, place =>
            {
                var productPlace = place.ProductPlace;
                if (productPlace != null && productPlace.Count == 0) count++;
            });
            return count;
        }

        // Locks every slot on the shelf that is holding nothing. Slots that still have stock are left
        // working, so this is safe to press on a half-full shelf.
        public static int LockEmptySlots(ShelfProducts shelf)
        {
            int locked = 0;
            int unkeyable = 0;

            ForEachSlot(shelf, place =>
            {
                var productPlace = place.ProductPlace;
                if (productPlace == null || productPlace.Count > 0) return;

                var key = Key(place);
                if (key == null)
                {
                    unkeyable++;
                    return;
                }
                if (Locked.Add(key)) locked++;
            });

            if (unkeyable > 0)
                MelonLogger.Warning($"[ShelfLocks] {unkeyable} slot(s) have no persistent id and cannot be set aside.");

            if (locked > 0)
            {
                Save();
                MelonLogger.Msg($"[ShelfLocks] Set {locked} slot(s) aside on '{Name(shelf)}'. {Locked.Count} total.");
            }
            else
            {
                MelonLogger.Msg($"[ShelfLocks] Nothing new to set aside on '{Name(shelf)}'.");
            }
            return locked;
        }

        public static int Unlock(ShelfProducts shelf)
        {
            int freed = 0;
            ForEachSlot(shelf, place =>
            {
                var key = Key(place);
                if (key != null && Locked.Remove(key)) freed++;
            });

            if (freed > 0)
            {
                Save();
                MelonLogger.Msg($"[ShelfLocks] Released {freed} slot(s) on '{Name(shelf)}'. {Locked.Count} total.");
            }
            return freed;
        }

        private static void ForEachSlot(ShelfProducts shelf, Action<ProductPricePlace> visit)
        {
            if (shelf == null) return;
            try
            {
                var places = shelf.Places;
                if (places == null) return;
                for (int i = 0; i < places.Length; i++)
                    if (places[i] != null) visit(places[i]);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[ShelfLocks] Walking a shelf's slots failed: {ex.Message}");
            }
        }

        private static string Key(ProductPricePlace place)
        {
            if (place == null) return null;
            try
            {
                // PersistentId is the game's own slot key; the GUID is the fallback for shelves that
                // were never set up as building places.
                var id = place.PersistentId;
                if (!string.IsNullOrEmpty(id)) return id;

                var text = place.PersistentGuid.ToString();
                if (!string.IsNullOrEmpty(text) && text != Il2CppSystem.Guid.Empty.ToString()) return text;
            }
            catch (Exception ex)
            {
                if (!_warnedAboutKeys)
                {
                    _warnedAboutKeys = true;
                    MelonLogger.Warning($"[ShelfLocks] Could not read a slot's persistent id: {ex.GetType().Name}");
                }
            }
            return null;
        }

        private static string Name(ShelfProducts shelf)
        {
            try
            {
                return shelf == null ? "?" : shelf.gameObject.name;
            }
            catch
            {
                return "?";
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

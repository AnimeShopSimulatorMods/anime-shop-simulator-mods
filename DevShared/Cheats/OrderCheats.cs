using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppProject.Code.Gameplay.Controllers;
using Il2CppProject.Code.Gameplay.Definitions;
using Il2CppProject.Code.Gameplay.Player.Pickups;
using PickupType = Il2CppProject.Code.Gameplay.Definitions.PickupType;
using Il2CppProject.Code.Gameplay.Services;
using MelonLoader;

namespace AnimeShopMods.Dev.Cheats
{
    // Free deliveries. SpawnOrders drops boxes straight onto the delivery point without touching the
    // basket or the wallet, which is what "unlimited ordering" actually means here.
    //
    // The game keeps two order catalogs, not one: the ordinary shop stock, and the decor sold for the
    // second floor, which lives in its own list on ProductsConfig and is spawned under its own catalog
    // type. Reading only the first list left that decor out of the menu entirely, and the catalog type
    // has to match the definition or the spawn is rejected, so both are handled here.
    internal static class OrderCheats
    {
        public const int MaxPerBurst = 200;

        internal sealed class BoxEntry
        {
            public int Id;
            public string Label;
        }

        private static List<BoxEntry> _catalog;

        public static void InvalidateCatalog() => _catalog = null;

        public static List<BoxEntry> Catalog()
        {
            if (_catalog != null) return _catalog;

            var list = new List<BoxEntry>();
            try
            {
                var products = GameAccess.Products;
                if (products == null) return list;

                // Calling this builds the definition lists if they have not been built yet; the
                // returned view is unusable through Il2CppInterop, so the fields are read instead.
                products.PickupDefinitions();

                var seen = new HashSet<int>();
                int fromShop = Collect(list, seen, products._pickupDefinitions, false);
                int fromDecor = Collect(list, seen, products._decor2FloorDefinitions, true);

                list.Sort((a, b) => string.CompareOrdinal(a.Label, b.Label));
                _catalog = list;
                DevLog.Log($"[CheatForDev] Box catalog built: {list.Count} entries " +
                         $"({fromShop} from the shop list, {fromDecor} more from the second-floor decor list).");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[CheatForDev] Reading the box catalog failed: {ex.Message}");
            }
            return list;
        }

        private static int Collect(List<BoxEntry> list, HashSet<int> seen,
                                   Il2CppSystem.Collections.Generic.List<PickupDefinition> definitions,
                                   bool secondFloorDecor)
        {
            if (definitions == null) return 0;

            int added = 0;
            for (int i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i];
                if (definition == null) continue;
                if (!seen.Add(definition.Id)) continue;
                list.Add(new BoxEntry { Id = definition.Id, Label = Label(definition, secondFloorDecor) });
                added++;
            }
            return added;
        }

        public static void Spawn(int pickupDefinitionId, int count)
        {
            if (!Guard()) return;
            if (count <= 0) return;

            count = Math.Min(count, MaxPerBurst);

            var definition = FindDefinition(pickupDefinitionId);
            if (definition == null)
            {
                MelonLogger.Warning($"[CheatForDev] No box definition with id {pickupDefinitionId}; " +
                                    "nothing was spawned. The catalog is probably stale - reload the save.");
                return;
            }

            float moneyBefore = ProgressCheats.Get(ParameterType.Money);
            int spawned = SpawnDirect(pickupDefinitionId, count);

            if (spawned == 0)
            {
                MelonLogger.Warning($"[CheatForDev] Could not place any '{definition.Name}' " +
                                    $"(id {pickupDefinitionId}). Nothing was spawned.");
                return;
            }

            RefundIfCharged(moneyBefore, definition.Name);

            if (spawned < count)
                MelonLogger.Warning($"[CheatForDev] Asked for {count} '{definition.Name}' but only " +
                                    $"{spawned} could be placed; the delivery point is probably full.");
            else
                DevLog.Log($"[CheatForDev] Spawned {spawned} box(es) of '{definition.Name}' " +
                         $"(id {pickupDefinitionId}, type {definition.Type}).");
        }

        // SpawnOrders looks like the free route but is not: it runs the order through the game's own
        // validation and pricing, so it rejects anything the shop catalog does not currently sell - which
        // is why picking a manga spawned nothing at all - and it debits the wallet on the way through.
        // Neither is wanted here. Nobody walked to the computer and paid; the box was conjured from a
        // cheat menu, so it is created straight onto the delivery point instead.
        private static int SpawnDirect(int pickupDefinitionId, int count)
        {
            int spawned = 0;
            try
            {
                var orders = GameAccess.Orders;
                var points = orders.ResolveSpawnPoints(false);
                if (points == null || points.Count == 0)
                {
                    MelonLogger.Warning("[CheatForDev] The game reports no delivery spawn points.");
                    return 0;
                }

                for (int i = 0; i < count; i++)
                {
                    var point = points[i % points.Count];
                    if (point == null) continue;

                    var position = orders.GetPosition(point.transform);
                    var pickup = orders.CreateNewPickup(pickupDefinitionId, position, point.transform.rotation);
                    if (pickup == null) continue;

                    orders.RegisterOrderPickup(pickup);
                    spawned++;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[CheatForDev] Placing boxes failed after {spawned}: {ex}");
            }
            return spawned;
        }

        // Belt and braces. Creating the pickup directly should never touch the wallet, but a free
        // delivery that quietly bills the player is the kind of thing nobody notices for a release, so
        // the balance is compared and put back if it moved.
        private static void RefundIfCharged(float moneyBefore, string what)
        {
            try
            {
                float moneyAfter = ProgressCheats.Get(ParameterType.Money);
                float charged = moneyBefore - moneyAfter;
                if (charged <= 0f) return;

                ProgressCheats.Set(ParameterType.Money, moneyBefore);
                MelonLogger.Warning($"[CheatForDev] Spawning '{what}' charged {charged}; refunded. " +
                                    "Deliveries from this menu are free.");
            }
            catch (Exception ex)
            {
                DevLog.Log($"[CheatForDev] Could not check the wallet after spawning ({ex.GetType().Name}).");
            }
        }

        private static string Label(PickupDefinition definition, bool secondFloorDecor)
        {
            try
            {
                string name = definition.Name;
                if (string.IsNullOrEmpty(name)) name = $"#{definition.Id}";
                string locked = definition.NotAvailable ? " (locked)" : string.Empty;
                string where = secondFloorDecor ? " (2nd floor)" : string.Empty;
                return $"{name} [{definition.Type}] #{definition.Id}{where}{locked}";
            }
            catch
            {
                return $"#{definition.Id}";
            }
        }

        // ---------------------------------------------------------------------- clearing up

        // Spawning two hundred boxes is one button press; picking them back up one at a time is not, so
        // there has to be a way out. Three things are left alone on purpose: quest deliveries, because
        // removing one strands the quest that wants it; anything locked; and anything sitting in a pickup
        // place, which covers stocked shelves and boxes held in hands. What is left is loose clutter.
        internal sealed class ClearResult
        {
            public int Removed;
            public int SkippedQuest;
            public int SkippedInPlace;
            public int SkippedNotABox;
            public int Failed;
        }

        public static int LooseBoxCount()
        {
            int count = 0;
            try
            {
                var pickups = UnityEngine.Object.FindObjectsOfType<Pickup>();
                if (pickups == null) return 0;

                var tracked = TrackedOrderPickups();
                foreach (var pickup in pickups)
                    if (IsLooseClutter(pickup) && tracked.Contains(pickup.GetInstanceID())) count++;
            }
            catch
            {
                return 0;
            }
            return count;
        }

        // A furniture crate waiting to be opened and a shelf already standing in the shop are the same
        // PickupType, so the category cannot tell them apart - which is why clearing by category either
        // spared the crates or deleted the shop. What does tell them apart is whether the order system
        // still tracks the pickup: it hands one out on delivery and lets go of it once the thing is
        // built. Crates are still on that list, fittings are not.
        private static HashSet<int> TrackedOrderPickups()
        {
            var tracked = new HashSet<int>();
            try
            {
                var registry = GameAccess.Orders._pickups;
                if (registry == null) return tracked;

                for (int i = 0; i < registry.Count; i++)
                {
                    var pickup = registry[i];
                    if (pickup != null) tracked.Add(pickup.GetInstanceID());
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[CheatForDev] Could not read the order registry ({ex.GetType().Name}); " +
                                    "furniture crates will be left alone.");
            }
            return tracked;
        }

        public static int FurnitureCrateCount()
        {
            try
            {
                var tracked = TrackedOrderPickups();
                if (tracked.Count == 0) return 0;

                int count = 0;
                var pickups = UnityEngine.Object.FindObjectsOfType<Pickup>();
                if (pickups == null) return 0;

                foreach (var pickup in pickups)
                    if (IsClearableFurnitureCrate(pickup, tracked)) count++;
                return count;
            }
            catch
            {
                return 0;
            }
        }

        private static bool IsClearableFurnitureCrate(Pickup pickup, HashSet<int> tracked)
        {
            try
            {
                if (pickup == null || !pickup.IsValid()) return false;
                if (pickup.IsLocked || pickup.IsInPickupPlace) return false;
                if (IsQuestDelivery(pickup)) return false;

                var definition = pickup.Definition;
                if (definition == null || definition.Type != PickupType.Furniture) return false;

                // Still on the delivery list means still a crate. Anything the order system has let go
                // of has been built into the shop and is not ours to remove.
                return tracked.Contains(pickup.GetInstanceID());
            }
            catch
            {
                return false;
            }
        }

        public static ClearResult ClearFurnitureCrates()
        {
            var result = new ClearResult();
            if (!Guard()) return result;

            MelonLogger.Msg("[CheatForDev] Clearing undelivered furniture crates.");
            try
            {
                var tracked = TrackedOrderPickups();
                var pickups = UnityEngine.Object.FindObjectsOfType<Pickup>();
                if (pickups == null || tracked.Count == 0)
                {
                    MelonLogger.Msg("[CheatForDev] There are no furniture crates on the delivery list.");
                    return result;
                }

                var orders = GameAccess.Orders;
                var doomed = new Il2CppSystem.Collections.Generic.List<Pickup>();
                int placed = 0;

                foreach (var pickup in pickups)
                {
                    if (pickup == null) continue;
                    if (IsClearableFurnitureCrate(pickup, tracked)) { doomed.Add(pickup); continue; }

                    try
                    {
                        var definition = pickup.Definition;
                        if (definition != null && definition.Type == PickupType.Furniture) placed++;
                    }
                    catch { /* counted as neither; it is not being touched either way */ }
                }

                MelonLogger.Msg($"  {doomed.Count} crate(s) still on the delivery list, " +
                                $"{placed} fitting(s) already built into the shop and left alone.");

                if (doomed.Count == 0)
                {
                    SweepPickups(pickups);
                    return result;
                }

                int before = doomed.Count;
                try
                {
                    orders.RollbackSpawnedPickups(doomed);
                    result.Removed = before;
                }
                catch (Exception ex)
                {
                    DevLog.Log($"[CheatForDev]   rollback was unavailable ({ex.GetType().Name}); " +
                             "despawning one at a time instead.");
                    result.Removed = DespawnEachSeparately(orders, doomed, ref result);
                }

                result.SkippedNotABox = placed;
                MelonLogger.Msg($"[CheatForDev] Removed {result.Removed} furniture crate(s).");
                ReportSkips(result);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[CheatForDev] Clearing furniture crates failed: {ex}");
            }
            return result;
        }

        public static ClearResult ClearAll() => Clear(null, "every loose box");

        public static ClearResult ClearOfType(int pickupDefinitionId)
        {
            var definition = FindDefinition(pickupDefinitionId);
            string what = definition == null ? $"id {pickupDefinitionId}" : $"'{definition.Name}'";
            return Clear(pickupDefinitionId, what);
        }

        private static ClearResult Clear(int? onlyDefinitionId, string what)
        {
            var result = new ClearResult();
            if (!Guard()) return result;

            MelonLogger.Msg($"[CheatForDev] Clearing {what} from the floor.");
            try
            {
                var pickups = UnityEngine.Object.FindObjectsOfType<Pickup>();
                if (pickups == null || pickups.Length == 0)
                {
                    MelonLogger.Msg("[CheatForDev] There are no boxes in the world.");
                    return result;
                }

                var orders = GameAccess.Orders;
                var doomed = new Il2CppSystem.Collections.Generic.List<Pickup>();
                var tracked = TrackedOrderPickups();

                foreach (var pickup in pickups)
                {
                    if (pickup == null) continue;

                    if (IsQuestDelivery(pickup)) { result.SkippedQuest++; continue; }
                    if (IsProtectedFitting(pickup)) { result.SkippedNotABox++; continue; }
                    if (!IsLooseClutter(pickup)) { result.SkippedInPlace++; continue; }

                    // The order system lets go of a pickup once it has been built into the shop, so
                    // anything missing from its list is part of the shop now, whatever its type says.
                    if (!tracked.Contains(pickup.GetInstanceID())) { result.SkippedNotABox++; continue; }

                    if (onlyDefinitionId.HasValue && !HasDefinitionId(pickup, onlyDefinitionId.Value)) continue;

                    doomed.Add(pickup);
                }

                if (doomed.Count == 0)
                {
                    MelonLogger.Msg($"[CheatForDev] Nothing matching {what} was loose on the floor.");
                    ReportSkips(result);
                    SweepPickups(pickups);
                    return result;
                }

                // The game's own cleanup for an order it decided to undo: it unregisters each pickup and
                // takes it out of the save as well as despawning it, which is exactly what is wanted.
                int before = doomed.Count;
                try
                {
                    orders.RollbackSpawnedPickups(doomed);
                    result.Removed = before;
                    DevLog.Log($"[CheatForDev]   removed {before} via the game's own rollback.");
                }
                catch (Exception ex)
                {
                    DevLog.Log($"[CheatForDev]   rollback was unavailable ({ex.GetType().Name}); " +
                             "despawning one at a time instead.");
                    result.Removed = DespawnEachSeparately(orders, doomed, ref result);
                }

                MelonLogger.Msg($"[CheatForDev] Removed {result.Removed} box(es).");
                ReportSkips(result);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[CheatForDev] Clearing boxes failed: {ex}");
            }
            return result;
        }

        private static int DespawnEachSeparately(OrderController orders,
                                                 Il2CppSystem.Collections.Generic.List<Pickup> doomed,
                                                 ref ClearResult result)
        {
            int removed = 0;
            var network = GameAccess.Service<NetworkService>() ?? orders._networkService;
            if (network == null)
            {
                MelonLogger.Warning("[CheatForDev] No network service, so nothing could be despawned.");
                return 0;
            }

            for (int i = 0; i < doomed.Count; i++)
            {
                var pickup = doomed[i];
                if (pickup == null) continue;
                try
                {
                    orders.UnregisterOrderPickup(pickup);
                    network.DespawnNetworkObject(pickup.NetworkObject);
                    removed++;
                }
                catch
                {
                    result.Failed++;
                }
            }
            return removed;
        }

        private static void ReportSkips(ClearResult result)
        {
            if (result.SkippedQuest > 0)
                MelonLogger.Msg($"[CheatForDev]   left {result.SkippedQuest} quest delivery box(es) alone.");
            if (result.SkippedInPlace > 0)
                DevLog.Log($"[CheatForDev]   left {result.SkippedInPlace} box(es) that are on a shelf or in hands.");
            if (result.SkippedNotABox > 0)
                DevLog.Log($"[CheatForDev]   left {result.SkippedNotABox} item(s) alone: shop fittings, anything " +
                         "already built in, and anything the game could not identify.");
            if (result.Failed > 0)
                MelonLogger.Warning($"[CheatForDev]   {result.Failed} box(es) refused to despawn.");
        }

        // The shop's fittings are Pickups too. A shelf, a till and a monitor are all PickupType.Furniture
        // sitting on the floor exactly like a delivered box, so a filter of "not in a pickup place" swept
        // them up along with the clutter. Only products are ever cleared now, and anything carrying a
        // building piece is left alone on top of that, because that is what "can be placed in the shop"
        // means here.
        // Named for what it protects rather than what it allows. Requiring PickupType.Product and no
        // building piece turned out to reject every pickup in the shop, so the rule is the other way
        // round now: name the two categories that must never be deleted - the fittings and the licence
        // pickups - and let anything else through. A denylist cannot quietly exclude everything the way
        // that allowlist did, and the categories it names are exactly the ones that caused the damage.
        private static bool IsProtectedFitting(Pickup pickup)
        {
            try
            {
                var definition = pickup.Definition;

                // No definition means the pickup cannot be identified, and an unidentified object in the
                // player's shop is protected. Returning "not protected" here is what let the manager's
                // desk be deleted: the catch below already said unknown things are left alone, and this
                // path quietly did the opposite.
                if (definition == null) return true;

                return definition.Type == PickupType.Furniture || definition.Type == PickupType.Brand;
            }
            catch
            {
                // Unreadable means unknown, and unknown things are left alone.
                return true;
            }
        }

        // Finding nothing to clear on a floor that is visibly covered in boxes means the filter is
        // wrong, not that the floor is clean. Rather than guess at which test is rejecting them, print
        // what every pickup in the scene actually looks like to that filter.
        private static void SweepPickups(Il2CppArrayBase<Pickup> pickups)
        {
            MelonLogger.Msg("===== [CheatForDev] pickup sweep =====");
            MelonLogger.Msg($"  {pickups.Length} pickup(s) in the scene. Columns: type, building piece, " +
                            "in a place, quest, locked.");

            int shown = 0;
            var counts = new Dictionary<string, int>();

            foreach (var pickup in pickups)
            {
                if (pickup == null) continue;

                string type, building, inPlace, quest, locked, name;
                try { name = pickup.Definition == null ? "(no definition)" : pickup.Definition.Name; }
                catch (Exception ex) { name = $"(threw {ex.GetType().Name})"; }
                try { type = pickup.Definition == null ? "(null)" : pickup.Definition.Type.ToString(); }
                catch (Exception ex) { type = $"(threw {ex.GetType().Name})"; }
                try { building = pickup.HasBuildingPiece ? "yes" : "no"; }
                catch (Exception ex) { building = $"(threw {ex.GetType().Name})"; }
                try { inPlace = pickup.IsInPickupPlace ? "yes" : "no"; }
                catch (Exception ex) { inPlace = $"(threw {ex.GetType().Name})"; }
                try { quest = pickup.IsQuestDelivery ? "yes" : "no"; }
                catch (Exception ex) { quest = $"(threw {ex.GetType().Name})"; }
                try { locked = pickup.IsLocked ? "yes" : "no"; }
                catch (Exception ex) { locked = $"(threw {ex.GetType().Name})"; }

                string key = $"type={type} building={building} inPlace={inPlace} quest={quest} locked={locked}";
                counts.TryGetValue(key, out int seen);
                counts[key] = seen + 1;

                // A few worked examples, then the tallies; a hundred identical lines help nobody.
                if (shown < 12)
                {
                    MelonLogger.Msg($"  '{name}' -> {key}");
                    shown++;
                }
            }

            MelonLogger.Msg("  tallies:");
            foreach (var pair in counts)
                MelonLogger.Msg($"    {pair.Value,4} x  {pair.Key}");
            MelonLogger.Msg("===== [CheatForDev] sweep end =====");
        }

        private static bool IsLooseClutter(Pickup pickup)
        {
            try
            {
                if (pickup == null || !pickup.IsValid()) return false;
                if (pickup.IsLocked) return false;
                if (pickup.IsInPickupPlace) return false;
                return !IsQuestDelivery(pickup) && !IsProtectedFitting(pickup);
            }
            catch
            {
                return false;
            }
        }

        private static bool IsQuestDelivery(Pickup pickup)
        {
            try
            {
                return pickup.IsQuestDelivery;
            }
            catch
            {
                // Better to treat an unreadable pickup as a quest box and leave it than to strand a quest.
                return true;
            }
        }

        private static bool HasDefinitionId(Pickup pickup, int pickupDefinitionId)
        {
            try
            {
                var definition = pickup.Definition;
                return definition != null && definition.Id == pickupDefinitionId;
            }
            catch
            {
                return false;
            }
        }

        // The game hands out definition ids from its own data files and nothing says they have to be
        // positive: plenty of them are not. Looking the id back up is the only way to tell "this box
        // cannot be spawned" apart from "this id just looks unusual", which is the mistake that hid
        // several products behind a button that was never drawn.
        public static PickupDefinition FindDefinition(int pickupDefinitionId)
        {
            try
            {
                var products = GameAccess.Products;
                if (products == null) return null;

                var found = products.FindPickupDefinition(pickupDefinitionId);
                if (found != null) return found;

                // Second-floor decor is not in the pickup dictionary the lookup above searches.
                var decor = products._decor2FloorDefinitions;
                if (decor == null) return null;

                for (int i = 0; i < decor.Count; i++)
                {
                    var definition = decor[i];
                    if (definition != null && definition.Id == pickupDefinitionId) return definition;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[CheatForDev] Looking up definition {pickupDefinitionId} failed: {ex.Message}");
            }
            return null;
        }

        private static bool Guard()
        {
            if (GameAccess.Orders == null) return false;
            if (GameAccess.IsServer) return true;
            MelonLogger.Warning("[CheatForDev] Only the host can spawn orders.");
            return false;
        }
    }
}

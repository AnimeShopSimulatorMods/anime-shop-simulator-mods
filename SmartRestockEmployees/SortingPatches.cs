using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2CppProject.Code.Gameplay.AI.Employee;
using Il2CppProject.Code.Gameplay.Interactions.Shelfs;
using Il2CppProject.Code.Gameplay.Player.Products;
using MelonLoader;
using UnityEngine;
using PickupIdMap = Il2CppSystem.Collections.Generic.Dictionary<int, byte>;
using PickupList = Il2CppSystem.Collections.Generic.List<Il2CppProject.Code.Gameplay.Player.Products.PickupProducts>;
using ShelfList = Il2CppSystem.Collections.Generic.List<Il2CppProject.Code.Gameplay.Interactions.Shelfs.ShelfProducts>;

namespace SmartRestockEmployees
{
    // How the sorting employee picks work (read out of the game's native code, verified on 1.0.4):
    //   FindShelf -> IsControllerRoleActive -> CollectAvailableShelves -> CollectAvailablePickups
    //             -> GetConfiguredPlace -> (only if that returned null) TryFindEmptyPlacementCandidate
    //             -> FindPickup
    //   FindNextProductPlace / TryRetargetCurrentPickup -> CollectAvailableShelves -> FindNextProductPlaceCandidate
    //
    // GetConfiguredPlace only looks at slots whose LastDefinitionId is non-zero, and picks the NEAREST one
    // that has a matching box. Slots that never held a product are reachable exclusively through
    // TryFindEmptyPlacementCandidate, which the game skips whenever GetConfiguredPlace found something.
    // That is the lever this mod uses: hide every slot except the one it wants, and the game's own code
    // lands on it, falling through to the empty-placement path by itself when the wanted slot is one that
    // was never stocked.
    //
    // Methods with ref/out parameters are deliberately NOT patched: Il2CppInterop's native->managed
    // trampoline reads a float& with a pointer-sized load and rewrites it, corrupting the game's value
    // even when the patch does nothing. Instead, a search context is opened around the parameterless
    // entry points, the by-value candidate lists are filtered, and HavePoints hides slots that must not
    // be chosen.
    internal sealed class SearchContext
    {
        public enum SearchKind { FindShelf, NextPlace }

        public IntPtr Controller;
        public SearchKind Kind;
        public int ProductId;              // NextPlace: product in the carried box
        public bool Restricting;           // FindShelf: candidate lists are ready, HavePoints may filter
        public bool Bypass;                // while this mod itself queries HavePoints
        public ShelfList Shelves;
        public IntPtr TargetPlace;         // FindShelf: the only slot the game may pick
        public int TargetProductId;

        private static readonly Stack<SearchContext> Stack = new Stack<SearchContext>();
        private const int MaxDepth = 8;

        public static SearchContext Current => Stack.Count > 0 ? Stack.Peek() : null;

        public static SearchContext Push(IntPtr controller, SearchKind kind)
        {
            if (Stack.Count >= MaxDepth)
            {
                MelonLogger.Warning("[SmartSorting] Search context stack overflowed; resetting.");
                Stack.Clear();
            }
            var context = new SearchContext { Controller = controller, Kind = kind };
            Stack.Push(context);
            return context;
        }

        public static SearchContext Pop(IntPtr controller, SearchKind kind)
        {
            if (Stack.Count == 0) return null;
            var top = Stack.Peek();
            if (top.Controller != controller || top.Kind != kind) return null;
            return Stack.Pop();
        }

        public static void Reset() => Stack.Clear();

        public static SearchContext For(IntPtr controller)
        {
            var current = Current;
            return current != null && current.Controller == controller ? current : null;
        }
    }

    // Slots that failed as a forced target recently, so the employee is not locked out of work.
    internal static class TargetBlacklist
    {
        private const float Seconds = 30f;
        private static readonly Dictionary<IntPtr, float> Until = new Dictionary<IntPtr, float>();

        public static void Add(IntPtr place) => Until[place] = Time.time + Seconds;

        public static bool Contains(IntPtr place)
        {
            if (!Until.TryGetValue(place, out var until)) return false;
            if (Time.time < until) return true;
            Until.Remove(place);
            return false;
        }

        public static void Reset() => Until.Clear();
    }

    // Which slot each sorting employee is on its way to, so two of them never pick the same one.
    // The controller only stores its target once the whole search succeeded, so the claim covers the gap.
    internal static class TargetClaims
    {
        private const float Seconds = 45f;

        private static readonly Dictionary<IntPtr, IntPtr> OwnerOfPlace = new Dictionary<IntPtr, IntPtr>();
        private static readonly Dictionary<IntPtr, float> ExpiresAt = new Dictionary<IntPtr, float>();

        public static void Reset()
        {
            OwnerOfPlace.Clear();
            ExpiresAt.Clear();
        }

        public static void Claim(IntPtr place, IntPtr owner)
        {
            ReleaseOwner(owner);
            OwnerOfPlace[place] = owner;
            ExpiresAt[place] = Time.time + Seconds;
        }

        public static void ReleaseOwner(IntPtr owner)
        {
            List<IntPtr> drop = null;
            foreach (var pair in OwnerOfPlace)
            {
                if (pair.Value != owner) continue;
                if (drop == null) drop = new List<IntPtr>();
                drop.Add(pair.Key);
            }
            if (drop == null) return;
            foreach (var place in drop)
            {
                OwnerOfPlace.Remove(place);
                ExpiresAt.Remove(place);
            }
        }

        public static bool HeldByOther(IntPtr place, IntPtr self)
        {
            if (!OwnerOfPlace.TryGetValue(place, out var owner)) return false;
            if (ExpiresAt.TryGetValue(place, out var until) && Time.time >= until)
            {
                OwnerOfPlace.Remove(place);
                ExpiresAt.Remove(place);
                return false;
            }
            return owner != self;
        }

        // The slots other sorting employees are actually standing at or walking to right now.
        public static HashSet<IntPtr> LiveTargetsOfOthers(IntPtr self)
        {
            var taken = new HashSet<IntPtr>();
            try
            {
                foreach (var other in UnityEngine.Object.FindObjectsOfType<EmployeeSortingController>())
                {
                    if (other == null || other.Pointer == self) continue;
                    var place = other._productPricePlace;
                    if (place != null) taken.Add(place.Pointer);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[SmartSorting] Could not read the other employees' targets: {ex}");
            }
            return taken;
        }
    }

    [HarmonyPatch(typeof(EmployeeSortingController), "FindShelf")]
    public static class SortingFindShelfPatch
    {
        [HarmonyPrefix]
        public static void Prefix(EmployeeSortingController __instance)
        {
            SearchContext.Push(__instance.Pointer, SearchContext.SearchKind.FindShelf);
        }

        [HarmonyPostfix]
        public static void Postfix(EmployeeSortingController __instance)
        {
            var context = SearchContext.Pop(__instance.Pointer, SearchContext.SearchKind.FindShelf);
            if (context == null || context.TargetPlace == IntPtr.Zero) return;

            try
            {
                if (__instance._pickup == null)
                {
                    // The game could not use the forced slot (for example the box does not fit).
                    // Let it choose freely next time instead of retrying the same dead end.
                    TargetBlacklist.Add(context.TargetPlace);
                    TargetClaims.ReleaseOwner(__instance.Pointer);
                    Main.Log($"[SmartSorting] Target slot for product {context.TargetProductId} was not used; skipping it for a while.");
                    return;
                }

                var chosen = __instance._productPricePlace;
                if (chosen != null && chosen.Pointer != context.TargetPlace)
                {
                    // Should not happen while the filter is working; worth knowing about if it ever does.
                    Main.Log("[SmartSorting] Game picked a different slot than the one this mod aimed at.");
                    TargetClaims.Claim(chosen.Pointer, __instance.Pointer);
                    return;
                }

                TargetClaims.Claim(context.TargetPlace, __instance.Pointer);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[SmartSorting] FindShelf postfix failed: {ex}");
            }
        }

        [HarmonyFinalizer]
        public static Exception Finalizer(EmployeeSortingController __instance, Exception __exception)
        {
            if (__exception != null)
                SearchContext.Pop(__instance.Pointer, SearchContext.SearchKind.FindShelf);
            return __exception;
        }
    }

    [HarmonyPatch(typeof(EmployeeSortingController), "CollectAvailableShelves")]
    public static class SortingCollectShelvesPatch
    {
        [HarmonyPostfix]
        public static void Postfix(EmployeeSortingController __instance, ShelfList __0)
        {
            var context = SearchContext.For(__instance.Pointer);
            if (context != null)
                context.Shelves = __0;
        }
    }

    // Pick the slot this employee should restock, and keep only boxes of the product it needs.
    [HarmonyPatch(typeof(EmployeeSortingController), "CollectAvailablePickups")]
    public static class SortingCollectPickupsPatch
    {
        [HarmonyPostfix]
        public static void Postfix(EmployeeSortingController __instance, PickupList __0, PickupIdMap __1)
        {
            var context = SearchContext.For(__instance.Pointer);
            if (context == null || context.Kind != SearchContext.SearchKind.FindShelf) return;

            try
            {
                if (__0 != null && __1 != null && context.Shelves != null)
                    ChooseTarget(__instance, context, __0, __1);
            }
            catch (Exception ex)
            {
                context.TargetPlace = IntPtr.Zero;
                MelonLogger.Error($"[SmartSorting] Target selection failed, using the game's choice: {ex}");
            }
            finally
            {
                context.Restricting = true;
            }
        }

        private struct Candidate
        {
            public ProductPricePlace Place;
            public int ProductId;
            public int Tier;        // 0 = slot is completely empty, 1 = slot still has stock
            public float Fill;      // 0..1
            public float Distance;  // squared, only used to break ties

            public bool Beats(Candidate best, bool emptyFirst)
            {
                if (best.Place == null) return true;
                if (emptyFirst && Tier != best.Tier) return Tier < best.Tier;
                if (!Mathf.Approximately(Fill, best.Fill)) return Fill < best.Fill;
                return Distance < best.Distance;
            }
        }

        private static void ChooseTarget(EmployeeSortingController controller, SearchContext context,
            PickupList pickups, PickupIdMap pickupIds)
        {
            var self = controller.Pointer;
            var busy = TargetClaims.LiveTargetsOfOthers(self);
            var employeePosition = controller.transform.position;
            bool emptyFirst = Main.EmptyShelvesFirst;

            var best = default(Candidate);
            int considered = 0;

            context.Bypass = true;
            try
            {
                var shelves = context.Shelves;
                for (int s = 0; s < shelves.Count; s++)
                {
                    var places = shelves[s] == null ? null : shelves[s].Places;
                    if (places == null) continue;

                    for (int p = 0; p < places.Length; p++)
                    {
                        var place = places[p];
                        if (place == null || place.IsLocked || !place.HavePoints) continue;
                        if (TargetBlacklist.Contains(place.Pointer)) continue;
                        if (busy.Contains(place.Pointer)) continue;
                        if (TargetClaims.HeldByOther(place.Pointer, self)) continue;

                        int productId = ResolveProductForSlot(place, pickups, pickupIds);
                        if (!SlotRules.HasProduct(productId)) continue;

                        var productPlace = place.ProductPlace;
                        if (productPlace == null) continue;

                        int count = productPlace.Count;
                        int max = productPlace.MaxCount;
                        var candidate = new Candidate
                        {
                            Place = place,
                            ProductId = productId,
                            Tier = count == 0 ? 0 : 1,
                            Fill = max > 0 ? (float)count / max : (count > 0 ? 1f : 0f),
                            Distance = (place.transform.position - employeePosition).sqrMagnitude,
                        };

                        considered++;
                        if (candidate.Beats(best, emptyFirst))
                            best = candidate;
                    }
                }
            }
            finally
            {
                context.Bypass = false;
            }

            if (best.Place == null)
            {
                Main.Log("[SmartSorting] No slot worth restocking; leaving the choice to the game.");
                return;
            }

            // Keep only boxes of the chosen product so the game's own pickup search cannot drift.
            var kept = new List<PickupProducts>();
            for (int i = 0; i < pickups.Count; i++)
            {
                var pickup = pickups[i];
                if (pickup != null && SlotRules.GetPickupProductId(pickup) == best.ProductId)
                    kept.Add(pickup);
            }
            if (kept.Count == 0) return;

            byte idValue = pickupIds[best.ProductId];
            pickups.Clear();
            foreach (var pickup in kept)
                pickups.Add(pickup);
            pickupIds.Clear();
            pickupIds.Add(best.ProductId, idValue);

            context.TargetPlace = best.Place.Pointer;
            context.TargetProductId = best.ProductId;
            TargetClaims.Claim(context.TargetPlace, self);

            string state = best.Tier == 0 ? "empty slot" : "fill " + best.Fill.ToString("0.##");
            Main.Log($"[SmartSorting] Target: product {best.ProductId}, {state}, {considered} candidate(s), {kept.Count} box(es).");
        }

        // A slot that already belongs to a product can only take that product back.
        // A slot that never held anything takes any product in storage that fits on it.
        private static int ResolveProductForSlot(ProductPricePlace place, PickupList pickups, PickupIdMap pickupIds)
        {
            int slotProductId = SlotRules.GetSlotProductId(place);
            if (SlotRules.HasProduct(slotProductId))
                return pickupIds.ContainsKey(slotProductId) ? slotProductId : SlotRules.NoProduct;

            var productPlace = place.ProductPlace;
            if (productPlace == null) return SlotRules.NoProduct;

            for (int i = 0; i < pickups.Count; i++)
            {
                var definition = pickups[i] == null ? null : pickups[i].ProductDefinition;
                if (definition == null) continue;
                if (productPlace.CanPut(definition.Type)) return definition.Id;
            }
            return SlotRules.NoProduct;
        }
    }

    // Leftover items in the carried box: never put them into a slot that belongs to another product.
    [HarmonyPatch(typeof(EmployeeSortingController), "FindNextProductPlace")]
    public static class SortingFindNextPlacePatch
    {
        [HarmonyPrefix]
        public static void Prefix(EmployeeSortingController __instance) => NextPlaceContext.Open(__instance);

        [HarmonyPostfix]
        public static void Postfix(EmployeeSortingController __instance) => NextPlaceContext.Close(__instance);

        [HarmonyFinalizer]
        public static Exception Finalizer(EmployeeSortingController __instance, Exception __exception)
        {
            if (__exception != null) NextPlaceContext.Close(__instance);
            return __exception;
        }
    }

    [HarmonyPatch(typeof(EmployeeSortingController), "TryRetargetCurrentPickup")]
    public static class SortingRetargetPatch
    {
        [HarmonyPrefix]
        public static void Prefix(EmployeeSortingController __instance) => NextPlaceContext.Open(__instance);

        [HarmonyPostfix]
        public static void Postfix(EmployeeSortingController __instance) => NextPlaceContext.Close(__instance);

        [HarmonyFinalizer]
        public static Exception Finalizer(EmployeeSortingController __instance, Exception __exception)
        {
            if (__exception != null) NextPlaceContext.Close(__instance);
            return __exception;
        }
    }

    internal static class NextPlaceContext
    {
        public static void Open(EmployeeSortingController controller)
        {
            var context = SearchContext.Push(controller.Pointer, SearchContext.SearchKind.NextPlace);
            try
            {
                context.ProductId = SlotRules.GetPickupProductId(controller._pickup);
            }
            catch (Exception ex)
            {
                context.ProductId = SlotRules.NoProduct;
                MelonLogger.Error($"[SmartSorting] Could not read carried product: {ex}");
            }
            context.Restricting = true;
        }

        public static void Close(EmployeeSortingController controller)
        {
            SearchContext.Pop(controller.Pointer, SearchContext.SearchKind.NextPlace);
        }
    }

    // The single hook every slot search goes through.
    [HarmonyPatch(typeof(ProductPricePlace), nameof(ProductPricePlace.HavePoints), MethodType.Getter)]
    public static class HavePointsPatch
    {
        [HarmonyPostfix]
        public static void Postfix(ProductPricePlace __instance, ref bool __result)
        {
            if (!__result) return;

            var context = SearchContext.Current;
            if (context == null || context.Bypass || !context.Restricting) return;

            try
            {
                __result = Allows(context, __instance);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[SmartSorting] HavePoints filter failed, allowing slot: {ex}");
            }
        }

        private static bool Allows(SearchContext context, ProductPricePlace place)
        {
            if (context.Kind == SearchContext.SearchKind.FindShelf && context.TargetPlace != IntPtr.Zero)
                return place.Pointer == context.TargetPlace;

            if (!Main.KeepEmptySlotProduct) return true;

            int slotProductId = SlotRules.GetSlotProductId(place);
            if (!SlotRules.HasProduct(slotProductId)) return true;

            if (context.Kind == SearchContext.SearchKind.NextPlace)
                return !SlotRules.HasProduct(context.ProductId) || slotProductId == context.ProductId;

            // FindShelf without a target: no box exists for any slot's own product, so an emptied slot stays reserved.
            return !SlotRules.IsEmpty(place);
        }
    }
}

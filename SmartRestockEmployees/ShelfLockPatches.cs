using System;
using HarmonyLib;
using Il2CppProject.Code.Gameplay.AI.Employee;
using Il2CppProject.Code.Gameplay.Player.Products;
using MelonLoader;

namespace SmartRestockEmployees
{
    // Keeping a set-aside slot empty, and knowing when the player has changed their mind about it.
    //
    // Telling the player apart from an employee cannot be done from the call stack: the employee's
    // search finishes long before it walks over and puts the item down, so by the time an item lands
    // there is no search running either way. What does separate them is that an employee placing
    // stock is standing at that exact slot with it as its current target, and a player never is.
    internal static class LockedSlots
    {
        public static bool EmployeeIsWorkingAt(ProductPricePlace place)
        {
            if (place == null) return false;
            try
            {
                foreach (var employee in UnityEngine.Object.FindObjectsOfType<EmployeeSortingController>())
                {
                    if (employee == null) continue;
                    var target = employee._productPricePlace;
                    if (target != null && target.Pointer == place.Pointer) return true;
                }
            }
            catch (Exception ex)
            {
                // Erring towards "the player did it" would wipe the lock on a bug; err the other way.
                MelonLogger.Error($"[ShelfLocks] Could not check who is at a slot: {ex.Message}");
                return true;
            }
            return false;
        }
    }

    // Stock appearing in a slot the player set aside means one of two things, and they are opposites.
    //
    // If the player put it there, they have changed their mind: the whole point of setting a slot
    // aside is to keep it empty, so filling it themselves ends that. The lock releases and employees
    // pick the slot back up, with no second trip to the panel to press Allow.
    //
    // If an employee put it there, the lock failed to hold and that is a bug worth shouting about.
    [HarmonyPatch(typeof(ProductPricePlace), "HandleOnProductAdded")]
    public static class LockedSlotFilledPatch
    {
        [HarmonyPostfix]
        public static void Postfix(ProductPricePlace __instance)
        {
            try
            {
                if (!ShelfLocks.IsLocked(__instance)) return;

                if (LockedSlots.EmployeeIsWorkingAt(__instance))
                {
                    MelonLogger.Warning("[ShelfLocks] An employee filled a slot that was set aside. " +
                                        "The lock did not hold; the slot stays set aside.");
                    return;
                }

                if (ShelfLocks.UnlockSlot(__instance))
                    MelonLogger.Msg("[ShelfLocks] You stocked a slot that was set aside, so employees will " +
                                    "look after it again.");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[ShelfLocks] Handling a filled slot failed: {ex.Message}");
            }
        }
    }

    // The second lever on the lock, and the reason there are two.
    //
    // HavePoints is where this mod hides slots from employees, and it demonstrably does not govern
    // every path: the game picks bare slots through TryFindEmptyPlacementCandidate, which judges them
    // on whether the product physically fits rather than on HavePoints. CanPut is the compatibility
    // question that path is built around, so refusing there covers what HavePoints misses.
    //
    // Gated on a live employee search on purpose. Outside one, CanPut is the game asking on someone
    // else's behalf -- including the player's own hands -- and a slot set aside must still accept
    // stock the player puts there, because that is how they release it.
    [HarmonyPatch(typeof(ProductPlace), nameof(ProductPlace.CanPut))]
    public static class LockedSlotCanPutPatch
    {
        [HarmonyPostfix]
        public static void Postfix(ProductPlace __instance, ref bool __result)
        {
            if (!__result) return;

            try
            {
                var context = SearchContext.Current;
                if (context == null || context.Bypass || !context.Restricting) return;

                var place = __instance.PricePlace;
                if (place != null && ShelfLocks.IsLocked(place)) __result = false;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[ShelfLocks] CanPut filter failed, allowing the slot: {ex.Message}");
            }
        }
    }
}

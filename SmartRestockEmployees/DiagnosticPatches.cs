using HarmonyLib;
using MelonLoader;
using Il2CppProject.Code.Gameplay.AI.Employee;
using Il2CppProject.Code.Gameplay.Player.Products;
using PickupList = Il2CppSystem.Collections.Generic.List<Il2CppProject.Code.Gameplay.Player.Products.PickupProducts>;

namespace SmartRestockEmployees
{
    // Read-only taps on the game's own restock search, to find out where it declines the slot this
    // mod hands it.
    //
    // Every method tapped here is free of ref/out parameters. GetConfiguredPlace and
    // TryFindEmptyPlacementCandidate are the two we would most like to watch, and both are absent on
    // purpose: Il2CppInterop's native->managed trampoline rewrites a by-ref argument with a
    // pointer-sized store, corrupting the game's value even when the patch body does nothing.
    //
    // Delete this file and Diagnostics.cs once the root cause is found.

    // Did the game get as far as asking for a box, and for which product?
    [HarmonyPatch(typeof(EmployeeSortingController), "FindPickup")]
    public static class DiagFindPickupPatch
    {
        [HarmonyPostfix]
        public static void Postfix(int __0, PickupList __1, PickupProducts __result)
        {
            if (!Diagnostics.Wants) return;
            int boxes = __1 == null ? -1 : __1.Count;
            Diagnostics.Step($"FindPickup(product {__0}, {boxes} box(es)) -> {(__result == null ? "NOTHING" : "a box")}");
        }
    }

    // New in 1.0.5. The mod's call-chain notes were written against 1.0.4 and do not know about it.
    [HarmonyPatch(typeof(EmployeeSortingController), "GetEmptyPlace")]
    public static class DiagGetEmptyPlacePatch
    {
        [HarmonyPostfix]
        public static void Postfix(ProductPricePlace __result)
        {
            if (!Diagnostics.Wants) return;
            Diagnostics.Step($"GetEmptyPlace -> {Diagnostics.Describe(__result)}");
        }
    }

    // The most likely gate to be rejecting the forced slot.
    [HarmonyPatch(typeof(EmployeeSortingController), "IsValidEmployeePlacement")]
    public static class DiagValidPlacementPatch
    {
        [HarmonyPostfix]
        public static void Postfix(ProductPricePlace __0, int __1, bool __result)
        {
            if (!Diagnostics.Wants) return;
            Diagnostics.Step($"IsValidEmployeePlacement({Diagnostics.Describe(__0)}, product {__1}) -> {__result}");
        }
    }

    // Counted rather than listed: this runs once per shelf, every pass.
    [HarmonyPatch(typeof(EmployeeSortingController), "HasAvailableShelfPlace")]
    public static class DiagShelfPlacePatch
    {
        [HarmonyPostfix]
        public static void Postfix(bool __result)
        {
            Diagnostics.ShelfCheck(__result);
        }
    }
}

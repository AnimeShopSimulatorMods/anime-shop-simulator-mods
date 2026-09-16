using HarmonyLib;
using Il2CppProject.Code.Gameplay.AI.Employee;
using Il2CppProject.Code.Gameplay.Interactions.Shelfs;
using Il2CppProject.Code.Gameplay.Player.Products;
using MelonLoader;

namespace SmartRestockEmployees
{
    // Storage employee: puts boxes onto the storage rack (ShelfPickups) with the fewest boxes.
    // The original FindShelf runs first so all of its bookkeeping (events, task state) happens as usual.
    // Afterwards the target is swapped to a less-filled rack, but only before the box is picked up,
    // so an employee already walking with a box is never redirected mid-task.
    [HarmonyPatch(typeof(EmployeeStorageController), "FindShelf")]
    public static class StorageLeastFilledRackPatch
    {
        [HarmonyPostfix]
        public static void Postfix(EmployeeStorageController __instance)
        {
            try
            {
                if (__instance._shelf == null || __instance._place == null || __instance._isPickupTaken)
                    return;

                var allControllers = UnityEngine.Object.FindObjectsOfType<EmployeeStorageController>();
                var allShelves = UnityEngine.Object.FindObjectsOfType<ShelfPickups>();

                ShelfPickups bestShelf = null;
                PickupPlace bestPlace = null;
                int lowestItemCount = CountBoxes(__instance._shelf);

                foreach (var shelf in allShelves)
                {
                    if (shelf == null || !shelf.HavePoints(false)) continue;

                    int currentItems = 0;
                    PickupPlace freePlace = null;

                    foreach (var place in shelf.Places)
                    {
                        if (place.Pickup != null)
                            currentItems++;
                        else if (freePlace == null && !IsTargetedByOther(place, __instance, allControllers))
                            freePlace = place;
                    }

                    if (freePlace != null && currentItems < lowestItemCount)
                    {
                        lowestItemCount = currentItems;
                        bestShelf = shelf;
                        bestPlace = freePlace;
                    }
                }

                if (bestShelf == null || bestShelf.Pointer == __instance._shelf.Pointer)
                    return;

                __instance.UnsubscribeFromShelfEvents();
                __instance._shelf = bestShelf;
                __instance._place = bestPlace;
                __instance.SubscribeToShelfEvents();
                Main.Log($"[SmartStorage] Retargeted to rack with {lowestItemCount} boxes.");
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"[SmartStorage] Postfix failed, keeping original target: {ex}");
            }
        }

        private static int CountBoxes(ShelfPickups shelf)
        {
            int count = 0;
            foreach (var place in shelf.Places)
                if (place.Pickup != null) count++;
            return count;
        }

        // Two storage employees must not walk to the same free spot.
        private static bool IsTargetedByOther(PickupPlace place, EmployeeStorageController self,
            Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppArrayBase<EmployeeStorageController> controllers)
        {
            foreach (var other in controllers)
            {
                if (other == null || other.Pointer == self.Pointer) continue;
                var target = other._place;
                if (target != null && target.Pointer == place.Pointer) return true;
            }
            return false;
        }
    }
}

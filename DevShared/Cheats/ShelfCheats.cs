using System;
using System.Collections.Generic;
using Il2CppProject.Code.Gameplay.Interactions.Shelfs;
using Il2CppProject.Code.Gameplay.Player.Products;
using MelonLoader;
using UnityEngine;

namespace AnimeShopMods.Dev.Cheats
{
    // Emptying sales shelves, either by deleting the stock outright or by turning it back into boxes
    // at the delivery point so it can be restocked again.
    internal static class ShelfCheats
    {
        internal sealed class ShelfEntry
        {
            public ShelfProducts Shelf;
            public string Label;
            public int ItemCount;
            public int SlotCount;
        }

        public static List<ShelfEntry> Scan()
        {
            var result = new List<ShelfEntry>();
            try
            {
                foreach (var shelf in UnityEngine.Object.FindObjectsOfType<ShelfProducts>())
                {
                    if (shelf == null) continue;
                    var entry = Describe(shelf);
                    if (entry != null) result.Add(entry);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[CheatForDev] Scanning shelves failed: {ex.Message}");
            }
            result.Sort((a, b) => string.CompareOrdinal(a.Label, b.Label));
            return result;
        }

        public static ShelfEntry Describe(ShelfProducts shelf)
        {
            if (shelf == null) return null;
            try
            {
                var places = shelf.Places;
                int items = 0;
                int slots = places == null ? 0 : places.Length;
                string product = null;

                for (int i = 0; i < slots; i++)
                {
                    var place = places[i];
                    var productPlace = place == null ? null : place.ProductPlace;
                    if (productPlace == null) continue;
                    items += productPlace.Count;
                    if (product == null && productPlace.Count > 0)
                        product = ProductName(place);
                }

                return new ShelfEntry
                {
                    Shelf = shelf,
                    Label = $"{shelf.gameObject.name} [{product ?? "empty"}]",
                    ItemCount = items,
                    SlotCount = slots,
                };
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[CheatForDev] Describing a shelf failed: {ex.Message}");
                return null;
            }
        }

        // What the player is looking at, so a shelf can be cleared without hunting for it in a list.
        public static ShelfProducts ShelfUnderCrosshair(float maxDistance = 6f)
        {
            try
            {
                var camera = Camera.main;
                if (camera == null) return null;

                var ray = new Ray(camera.transform.position, camera.transform.forward);
                var hits = Physics.RaycastAll(ray, maxDistance);
                if (hits == null) return null;

                ShelfProducts nearest = null;
                float nearestDistance = float.MaxValue;
                for (int i = 0; i < hits.Length; i++)
                {
                    var hit = hits[i];
                    if (hit.collider == null) continue;
                    var shelf = hit.collider.GetComponentInParent<ShelfProducts>();
                    if (shelf == null) continue;
                    if (hit.distance >= nearestDistance) continue;
                    nearestDistance = hit.distance;
                    nearest = shelf;
                }
                return nearest;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[CheatForDev] Looking for a shelf failed: {ex.Message}");
                return null;
            }
        }

        public static int Delete(ShelfProducts shelf)
        {
            if (!Guard() || shelf == null) return 0;

            int removed = 0;
            try
            {
                var places = shelf.Places;
                if (places == null) return 0;

                for (int i = 0; i < places.Length; i++)
                {
                    var productPlace = places[i] == null ? null : places[i].ProductPlace;
                    if (productPlace == null || productPlace.Count == 0) continue;
                    removed += EmptySlot(productPlace);
                }
                DevLog.Log($"[CheatForDev] Deleted {removed} item(s) from '{shelf.gameObject.name}'.");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[CheatForDev] Clearing a shelf failed: {ex}");
            }
            return removed;
        }

        public static int ReturnToDelivery(ShelfProducts shelf)
        {
            if (!Guard() || shelf == null) return 0;

            var orders = GameAccess.Orders;
            var products = GameAccess.Products;
            if (orders == null || products == null)
            {
                MelonLogger.Warning("[CheatForDev] Order controller or products config is unavailable; nothing returned.");
                return 0;
            }

            int returned = 0;
            try
            {
                var places = shelf.Places;
                if (places == null) return 0;

                for (int i = 0; i < places.Length; i++)
                {
                    var place = places[i];
                    var productPlace = place == null ? null : place.ProductPlace;
                    if (productPlace == null || productPlace.Count == 0) continue;

                    int count = productPlace.Count;
                    var productId = productPlace.Id;
                    int definitionId = DefinitionId(place);
                    if (definitionId <= 0)
                    {
                        MelonLogger.Warning($"[CheatForDev] Slot {i} has no product definition; deleting instead of returning.");
                        EmptySlot(productPlace);
                        continue;
                    }

                    var pickupDefinition = products.FindPickupDefinitionByProductId(definitionId);
                    if (pickupDefinition == null)
                    {
                        MelonLogger.Warning($"[CheatForDev] No box exists for product {definitionId}; deleting slot {i} instead.");
                        EmptySlot(productPlace);
                        continue;
                    }

                    if (!orders.TryGetRecoveryPose(out var position, out var rotation, true))
                    {
                        MelonLogger.Warning("[CheatForDev] The delivery point has no free space right now.");
                        return returned;
                    }

                    EmptySlot(productPlace);
                    orders.CreateShelfPickup(pickupDefinition.Id, position, rotation, count, productId, null, null, null);
                    returned += count;
                }
                DevLog.Log($"[CheatForDev] Returned {returned} item(s) from '{shelf.gameObject.name}' to the delivery point.");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[CheatForDev] Returning a shelf to the delivery point failed: {ex}");
            }
            return returned;
        }

        // Empties one slot one item at a time. SetCountServer(0) looks simpler but cannot be called from here:
        // its ProductTransferPayload parameter is a struct in the game, and the Il2CppInterop stub unboxes it
        // unconditionally, so the null default it advertises throws before the game is ever reached.
        // RemoveLastItemServer takes nothing and is the same path a customer taking the last item goes down.
        // The loop is bounded by the starting count, so it does not depend on Count updating synchronously.
        private static int EmptySlot(ProductPlace productPlace)
        {
            int start = productPlace.Count;
            for (int n = 0; n < start; n++)
                productPlace.RemoveLastItemServer();
            return start;
        }

        public static void DropRandomItem(ShelfProducts shelf)
        {
            if (!Guard() || shelf == null) return;
            try
            {
                shelf.DropSingleRandomItem();
                DevLog.Log($"[CheatForDev] Dropped an item from '{shelf.gameObject.name}'.");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[CheatForDev] Dropping an item failed: {ex}");
            }
        }

        private static int DefinitionId(ProductPricePlace place)
        {
            try
            {
                var info = place.ProductInfo;
                if (info != null && info.DefinitionId > 0) return info.DefinitionId;
                return place.LastDefinitionId;
            }
            catch
            {
                return 0;
            }
        }

        private static string ProductName(ProductPricePlace place)
        {
            try
            {
                int definitionId = DefinitionId(place);
                if (definitionId <= 0) return null;
                var products = GameAccess.Products;
                var definition = products == null ? null : products.FindProductDefinition(definitionId);
                return definition == null ? $"#{definitionId}" : definition.Name;
            }
            catch
            {
                return null;
            }
        }

        private static bool Guard()
        {
            if (GameAccess.IsServer) return true;
            MelonLogger.Warning("[CheatForDev] Only the host can change shelves.");
            return false;
        }
    }
}

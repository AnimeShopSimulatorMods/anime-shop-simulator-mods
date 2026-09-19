using System;
using Il2CppProject.Code.Core.Saves.SessionData;
using Il2CppProject.Code.Gameplay.Configs;
using Il2CppProject.Code.Gameplay.Controllers;
using Il2CppProject.Code.Gameplay.Interactions.Shelfs;
using Il2CppProject.Code.Gameplay.Player.Pickups;
using Il2CppProject.Code.Gameplay.Player.Products;
using MelonLoader;
using UnityEngine;

namespace AnimeShopMods.Game
{
    // Packing a shelf's stock back into boxes at a delivery zone, so a player can take a shelf out of
    // service without throwing the goods away.
    //
    // Zone 1 goes through the game's own TryGetRecoveryPose, which keeps its own stacking index.
    // Zone 2 has no API at all, so the pose is built by hand from OrderController._secondaryPoints and
    // handed to NormalizeSpawnPose for the game to snap into something legal.
    //
    // BuildSpawnAnchors and TryGetSpawnPose would be tidier for zone 2, and both are avoided on
    // purpose: they take a SpawnAnchor struct and an Il2CppStructArray, and struct marshalling through
    // Il2CppInterop is what broke SetCountServer in this codebase (see Empty below).
    internal static class DeliveryReturn
    {
        internal struct Result
        {
            public int Returned;
            public int Skipped;
            public bool ZoneWasFull;
            public bool FellBackToZoneOne;
        }

        public static Result Run(ShelfProducts shelf, OrderController orders, ProductsConfig products,
            bool secondZone)
        {
            var result = default(Result);
            if (shelf == null || orders == null || products == null)
            {
                MelonLogger.Warning("[DeliveryReturn] Missing shelf, order controller or product config.");
                return result;
            }

            if (secondZone && !HasSecondZone(orders))
            {
                secondZone = false;
                result.FellBackToZoneOne = true;
            }

            try
            {
                var places = shelf.Places;
                if (places == null) return result;

                int stackIndex = 0;
                for (int i = 0; i < places.Length; i++)
                {
                    var place = places[i];
                    var productPlace = place == null ? null : place.ProductPlace;
                    if (productPlace == null || productPlace.Count == 0) continue;

                    int count = productPlace.Count;
                    var productId = productPlace.Id;
                    int definitionId = ShelfMemory.SlotProductId(place);

                    var pickupDefinition = definitionId <= 0
                        ? null
                        : products.FindPickupDefinitionByProductId(definitionId);

                    // No box exists for this product. Leave the stock on the shelf and say so rather
                    // than deleting it, which is not something to do quietly to someone's store.
                    if (pickupDefinition == null)
                    {
                        result.Skipped += count;
                        continue;
                    }

                    if (!TryPose(orders, secondZone, stackIndex, definitionId,
                            out var position, out var rotation))
                    {
                        result.ZoneWasFull = true;
                        break;
                    }

                    Empty(productPlace);
                    var pickup = orders.CreateShelfPickup(pickupDefinition.Id, position, rotation, count,
                        productId, null, null, null);
                    Persist(orders, pickup, pickupDefinition.Id, count, productId, position, rotation);

                    result.Returned += count;
                    stackIndex++;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[DeliveryReturn] Returning a shelf failed: {ex}");
            }
            return result;
        }

        // CreateShelfPickup is the game's save-restore path -- note its RollbackShelfPickupRestore
        // sibling -- so it builds the box but never gives it save data. A box made that way looks
        // right, can be picked up, and is gone the next time the player loads. Attaching an
        // OrderSaveData and registering it is what a real order gets.
        //
        // _pickups and _orderSaveDatas are separate registries with their own add/remove pairs, so
        // both are needed. The Contains check is there in case RegisterOrderPickup already did the
        // second half: a duplicate entry would restore the same box twice.
        private static void Persist(OrderController orders, Pickup pickup, int definitionId, int count,
            Il2CppSystem.Guid productId, Vector3 position, Quaternion rotation)
        {
            if (pickup == null) return;

            try
            {
                if (pickup.orderSaveData != null) return;

                var saveData = new OrderSaveData
                {
                    Id = definitionId,
                    Count = count,
                    ProductId = productId,
                    Position = position,
                    Rotation = rotation,
                    IsDroppedPickup = false,
                };

                pickup.SetOrderSaveData(saveData);
                orders.RegisterOrderPickup(pickup);

                var saves = orders._orderSaveDatas;
                if (saves == null || !saves.Contains(saveData))
                    orders.AddOrderSave(saveData);

                pickup.UpdateSavePosition();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[DeliveryReturn] Could not make a returned box persist: {ex}");
            }
        }

        public static bool HasSecondZone(OrderController orders)
        {
            try
            {
                var points = orders == null ? null : orders._secondaryPoints;
                return points != null && points.Count > 0;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryPose(OrderController orders, bool secondZone, int stackIndex,
            int definitionId, out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;

            if (!secondZone)
                return orders.TryGetRecoveryPose(out position, out rotation, true);

            var points = orders._secondaryPoints;
            if (points == null || points.Count == 0) return false;

            var anchor = points[stackIndex % points.Count];
            var transform = anchor == null ? null : anchor.transform;
            if (transform == null) return false;

            // Spread across the zone's points first, then stack upwards once they have all been used.
            float step = OrderController.DefaultSpawnStackHeight + OrderController.SpawnStackClearance;
            int layer = stackIndex / points.Count;

            position = transform.position + Vector3.up * (step * layer);
            rotation = transform.rotation;
            orders.NormalizeSpawnPose(ref position, ref rotation, definitionId);
            return true;
        }

        // One item at a time. SetCountServer(0) looks simpler but cannot be called from here: its
        // ProductTransferPayload parameter is a struct, and the Il2CppInterop stub unboxes the null
        // default before the game is ever reached. RemoveLastItemServer takes nothing and is the same
        // path a customer taking the last item goes down. The loop is bounded by the starting count,
        // so it does not depend on Count updating synchronously.
        private static void Empty(ProductPlace productPlace)
        {
            int start = productPlace.Count;
            for (int n = 0; n < start; n++)
                productPlace.RemoveLastItemServer();
        }
    }
}

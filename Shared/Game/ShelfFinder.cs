using System;
using System.Collections.Generic;
using Il2CppProject.Code.Gameplay.Configs;
using Il2CppProject.Code.Gameplay.Interactions.Shelfs;
using Il2CppProject.Code.Gameplay.Player.Products;
using MelonLoader;
using UnityEngine;

namespace AnimeShopMods.Game
{
    // Finding shelves and describing them the way a player would: by the product on them and its own
    // icon. GameObject names never reach the screen -- nobody knows what Shelf_03 is.
    //
    // A shelf can hold more than one product at a time, so ProductName is the first product found and
    // ItemCount covers the whole shelf. That is what the panel needs; anything finer belongs per slot.
    internal static class ShelfFinder
    {
        internal sealed class ShelfEntry
        {
            public ShelfProducts Shelf;
            public string ProductName;
            public Texture ProductIcon;
            public int ItemCount;
            public int MaxCount;
            public int UsedSlots;
            public int SlotCount;
            public bool Remembers;
        }

        public static List<ShelfEntry> Scan(ProductsConfig products)
        {
            var result = new List<ShelfEntry>();
            try
            {
                foreach (var shelf in UnityEngine.Object.FindObjectsOfType<ShelfProducts>())
                {
                    if (shelf == null) continue;
                    var entry = Describe(shelf, products);
                    if (entry != null) result.Add(entry);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[ShelfFinder] Scanning shelves failed: {ex.Message}");
            }
            result.Sort((a, b) => string.CompareOrdinal(a.ProductName, b.ProductName));
            return result;
        }

        public static ShelfEntry Describe(ShelfProducts shelf, ProductsConfig products)
        {
            if (shelf == null) return null;
            try
            {
                var places = shelf.Places;
                int slots = places == null ? 0 : places.Length;
                int items = 0;
                int max = 0;
                int used = 0;
                bool remembers = false;
                string name = null;
                Texture icon = null;

                for (int i = 0; i < slots; i++)
                {
                    var place = places[i];
                    if (place == null) continue;

                    if (ShelfMemory.Remembers(place)) remembers = true;

                    var productPlace = place.ProductPlace;
                    if (productPlace == null) continue;

                    items += productPlace.Count;
                    max += productPlace.MaxCount;
                    if (productPlace.Count > 0) used++;

                    if (name != null) continue;
                    name = ProductName(place, products);
                    icon = ProductIcon(place);
                }

                return new ShelfEntry
                {
                    Shelf = shelf,
                    ProductName = name ?? "Empty shelf",
                    ProductIcon = icon,
                    ItemCount = items,
                    MaxCount = max,
                    UsedSlots = used,
                    SlotCount = slots,
                    Remembers = remembers,
                };
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[ShelfFinder] Describing a shelf failed: {ex.Message}");
                return null;
            }
        }

        public static ShelfProducts UnderCrosshair(float maxDistance = 6f)
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
                MelonLogger.Error($"[ShelfFinder] Looking for a shelf failed: {ex.Message}");
                return null;
            }
        }

        // The game's own look-highlight, the one the crosshair triggers. Shelves have no name a player
        // would recognise, so this is how a row in a list says which shelf it means.
        //
        // It has to be re-applied every frame: the game's own look raycast clears the flag on
        // everything it is not currently pointing at, so a single call flickers straight back off.
        public static void Highlight(ShelfProducts shelf)
        {
            if (shelf == null) return;
            try
            {
                var places = shelf.Places;
                if (places == null) return;
                for (int i = 0; i < places.Length; i++)
                    if (places[i] != null) places[i].EnableLook(true);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[ShelfFinder] Highlighting a shelf failed: {ex.Message}");
            }
        }

        private static Texture ProductIcon(ProductPricePlace place)
        {
            try
            {
                var sprite = place._currentIcon;
                return sprite == null ? null : sprite.texture;
            }
            catch
            {
                return null;
            }
        }

        private static string ProductName(ProductPricePlace place, ProductsConfig products)
        {
            try
            {
                int definitionId = ShelfMemory.SlotProductId(place);
                if (definitionId <= 0) return null;
                var definition = products == null ? null : products.FindProductDefinition(definitionId);
                return definition == null ? null : definition.Name;
            }
            catch
            {
                return null;
            }
        }
    }
}

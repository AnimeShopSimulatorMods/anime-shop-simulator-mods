using System;
using Il2CppProject.Code.Gameplay.Controllers;
using Il2CppProject.Code.Gameplay.Interactions.Shelfs;
using Il2CppProject.Code.Gameplay.Player.Products;
using MelonLoader;

namespace AnimeShopMods.Game
{
    // A shelf slot remembers the last product it held, and that memory is what keeps employees
    // refilling it after the stock runs out. Players who empty a shelf to reorganise their store have
    // no way to tell the game they meant it -- clearing the memory is that way.
    //
    // LastDefinitionId is get-only, but Il2CppInterop exposes the backing field as a settable property.
    internal static class ShelfMemory
    {
        public static int SlotProductId(ProductPricePlace place)
        {
            if (place == null) return 0;

            var productPlace = place.ProductPlace;
            if (productPlace != null && productPlace.Count > 0)
            {
                var info = place.ProductInfo;
                if (info != null && info.DefinitionId > 0) return info.DefinitionId;
            }

            return place.LastDefinitionId;
        }

        public static bool Remembers(ProductPricePlace place)
        {
            return SlotProductId(place) > 0;
        }

        // Returns how many slots actually had something to forget, so the caller can tell the player
        // "already forgotten" apart from "forgotten just now".
        //
        // Clearing _lastDefinitionId alone is not enough. That field is what this mod reads; the
        // product icon and price tag on the shelf are drawn from the slot's own binding -- the
        // _productId SyncVar, _productInfo and the slot's save data -- which the game keeps and can
        // re-derive the memory from. A half-cleared slot still shows its old tag and starts
        // remembering again, so the whole binding goes.
        //
        // The player's price is not touched: prices belong to the product, not the slot (see
        // EnsureShelfProductPrice, which is keyed by product id alone). Put the same product back on
        // the shelf later and its price is still there.
        public static int Clear(ShelfProducts shelf)
        {
            if (shelf == null) return 0;

            int cleared = 0;
            var places = shelf.Places;
            if (places == null) return 0;

            for (int i = 0; i < places.Length; i++)
            {
                var place = places[i];
                if (place == null) continue;

                try
                {
                    // Per slot, never per shelf. Stripping the tag off a slot that still holds stock
                    // would leave items sitting there with no price on them.
                    var productPlace = place.ProductPlace;
                    if (productPlace != null && productPlace.Count > 0) continue;

                    if (!Remembers(place) && !HasBinding(place)) continue;
                    Unassign(place);
                    cleared++;
                }
                catch (Exception ex)
                {
                    // One bad slot must not stop the rest of the shelf from being cleared.
                    MelonLogger.Error($"[ShelfMemory] Clearing slot {i} failed: {ex}");
                }
            }
            return cleared;
        }

        // The reverse of Clear: gives forgotten slots their memory back so employees start
        // restocking them again. Returns how many slots were actually given the memory, so the
        // caller can tell the player "restored" apart from "nothing here was forgotten".
        //
        // Only bare slots are touched -- empty, and not already remembering something -- so this
        // never overwrites a slot that is mid-use or already fine. For each one it sets
        // _lastDefinitionId, which alone is what makes employees consider the slot again, and then
        // tries to restore the icon and price tag the way Unassign strips them, in reverse.
        public static int Remember(ShelfProducts shelf, int definitionId)
        {
            if (shelf == null || definitionId <= 0) return 0;

            ProductInfo info = FindProduct(definitionId);

            int restored = 0;
            var places = shelf.Places;
            if (places == null) return 0;

            for (int i = 0; i < places.Length; i++)
            {
                var place = places[i];
                if (place == null) continue;

                try
                {
                    // Per slot, never per shelf. A slot already holding stock or already remembering
                    // a product is left exactly as it is.
                    var productPlace = place.ProductPlace;
                    if (productPlace == null || productPlace.Count > 0) continue;

                    if (Remembers(place) || HasBinding(place)) continue;

                    Assign(place, definitionId, info);
                    restored++;
                }
                catch (Exception ex)
                {
                    // One bad slot must not stop the rest of the shelf from being restored.
                    MelonLogger.Error($"[ShelfMemory] Restoring slot {i} failed: {ex}");
                }
            }
            return restored;
        }

        // A definition id worth restoring a forgotten slot with: whatever this shelf already sells,
        // stock or memory alike. 0 means the shelf has nothing of its own to go by.
        public static int ProductOnShelf(ShelfProducts shelf)
        {
            if (shelf == null) return 0;

            var places = shelf.Places;
            if (places == null) return 0;

            int remembered = 0;
            for (int i = 0; i < places.Length; i++)
            {
                var place = places[i];
                if (place == null) continue;

                var productPlace = place.ProductPlace;
                if (productPlace != null && productPlace.Count > 0)
                {
                    var info = place.ProductInfo;
                    if (info != null && info.DefinitionId > 0) return info.DefinitionId;
                }

                if (remembered == 0 && place.LastDefinitionId > 0) remembered = place.LastDefinitionId;
            }
            return remembered;
        }

        private static ProductInfo FindProduct(int definitionId)
        {
            try
            {
                var controller = UnityEngine.Object.FindObjectOfType<ProductsController>();
                var products = controller == null ? null : controller.Products;
                if (products == null) return null;

                // IReadOnlyList<T> only exposes its own indexer across the Il2Cpp boundary; Count
                // lives on IReadOnlyCollection<T>, which the same object implements underneath.
                int count = products.Cast<Il2CppSystem.Collections.Generic.IReadOnlyCollection<ProductInfo>>().Count;
                for (int i = 0; i < count; i++)
                {
                    var info = products[i];
                    if (info != null && info.DefinitionId == definitionId) return info;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[ShelfMemory] Looking up product {definitionId} failed: {ex.GetType().Name}");
            }
            return null;
        }

        private static void Assign(ProductPricePlace place, int definitionId, ProductInfo info)
        {
            place._lastDefinitionId = definitionId;

            if (info != null)
            {
                place._productInfo = info;
                try
                {
                    place._productId.Value = info.ProductId;
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[ShelfMemory] Could not restore the slot's product id: {ex.GetType().Name}");
                }
            }
            else
            {
                // Still worth doing: _lastDefinitionId alone is what makes employees restock the
                // slot. The price tag just will not reappear until the product is seen again.
                MelonLogger.Warning($"[ShelfMemory] Product {definitionId} was not found; the price tag will not come back on its own.");
            }

            Try(place.RefreshProductPresenceState, "RefreshProductPresenceState");
            Try(place.ApplyVisuals, "ApplyVisuals");

            place.EnsureProductSaveDataServer(true);
        }

        private static bool HasBinding(ProductPricePlace place)
        {
            try
            {
                return place._productId.Value != default(Il2CppSystem.Guid);
            }
            catch
            {
                return false;
            }
        }

        private static void Unassign(ProductPricePlace place)
        {
            place._lastDefinitionId = 0;
            place._productInfo = null;
            place._currentIcon = null;

            try
            {
                place._productId.Value = default(Il2CppSystem.Guid);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[ShelfMemory] Could not clear the slot's product id: {ex.GetType().Name}");
            }

            // Leave the save data with no product, or the binding comes back on the next load.
            var saveData = place._productSaveData;
            if (saveData != null)
            {
                saveData.ProductId = default(Il2CppSystem.Guid);
                saveData.Count = 0;
                saveData.StartCount = 0;
            }

            // Let the game redraw the slot from the cleared state: this is what takes the icon and
            // the price tag off the shelf.
            Try(() => place.SetIcon(null), "SetIcon");
            Try(place.RefreshProductPresenceState, "RefreshProductPresenceState");
            Try(place.ApplyVisuals, "ApplyVisuals");

            place.EnsureProductSaveDataServer(true);
        }

        private static void Try(Action call, string name)
        {
            try
            {
                call();
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[ShelfMemory] {name} failed: {ex.GetType().Name}");
            }
        }

    }
}

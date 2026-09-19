using System;
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

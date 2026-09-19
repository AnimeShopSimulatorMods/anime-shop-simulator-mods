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
        public static int Clear(ShelfProducts shelf)
        {
            if (shelf == null) return 0;

            int cleared = 0;
            try
            {
                var places = shelf.Places;
                if (places == null) return 0;

                for (int i = 0; i < places.Length; i++)
                {
                    var place = places[i];
                    if (place == null || place._lastDefinitionId == 0) continue;

                    place._lastDefinitionId = 0;
                    // Push the change into the slot's save data, or it comes back on the next load.
                    place.EnsureProductSaveDataServer(true);
                    cleared++;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[ShelfMemory] Clearing a shelf failed: {ex}");
            }
            return cleared;
        }
    }
}

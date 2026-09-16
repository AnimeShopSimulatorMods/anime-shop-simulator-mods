using Il2CppProject.Code.Gameplay.Player.Products;

namespace SmartRestockEmployees
{
    // Shared rules for deciding which product a sales-shelf slot belongs to.
    internal static class SlotRules
    {
        public const int NoProduct = 0;

        public static bool HasProduct(int definitionId) => definitionId > NoProduct;

        // The product a slot holds now, or the product it held before it ran empty.
        public static int GetSlotProductId(ProductPricePlace place)
        {
            if (place == null) return NoProduct;

            var productPlace = place.ProductPlace;
            if (productPlace != null && productPlace.Count > 0)
            {
                var info = place.ProductInfo;
                if (info != null && HasProduct(info.DefinitionId))
                    return info.DefinitionId;
            }

            return place.LastDefinitionId;
        }

        public static int GetPickupProductId(PickupProducts pickup)
        {
            var definition = pickup == null ? null : pickup.ProductDefinition;
            return definition == null ? NoProduct : definition.Id;
        }

        public static bool IsEmpty(ProductPricePlace place)
        {
            var productPlace = place.ProductPlace;
            return productPlace != null && productPlace.Count == 0;
        }
    }
}

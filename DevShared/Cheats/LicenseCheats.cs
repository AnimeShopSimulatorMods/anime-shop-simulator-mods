using System;
using System.Collections.Generic;
using Il2CppProject.Code.Gameplay.Controllers;
using Il2CppProject.Code.Gameplay.Definitions;
using Il2CppProject.Code.Gameplay.Services;
using MelonLoader;

namespace AnimeShopMods.Dev.Cheats
{
    // What the shop UI calls a licence the code calls a brand: PickupDefinition entries of PickupType.Brand,
    // tracked in ProductsController._brands. BuyBrandSystem is the game's own no-charge unlock, the same call
    // the tutorial uses, so nothing has to be faked here.
    internal static class LicenseCheats
    {
        internal sealed class LicenseEntry
        {
            public int Id;
            public string Label;
            public bool Owned;
        }

        public static List<LicenseEntry> All()
        {
            var result = new List<LicenseEntry>();
            try
            {
                var products = GameAccess.Products;
                if (products == null) return result;

                var definitions = products._brandDefinitions;
                if (definitions == null || definitions.Count == 0)
                {
                    // Older builds kept brands only in the combined pickup list.
                    var all = products._pickupDefinitions;
                    if (all == null) return result;
                    for (int i = 0; i < all.Count; i++)
                    {
                        var definition = all[i];
                        if (definition == null || definition.Type != PickupType.Brand) continue;
                        result.Add(Describe(definition));
                    }
                    return Sorted(result);
                }

                for (int i = 0; i < definitions.Count; i++)
                {
                    var definition = definitions[i];
                    if (definition == null) continue;
                    result.Add(Describe(definition));
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[CheatForDev] Reading the licence list failed: {ex.Message}");
            }
            return Sorted(result);
        }

        public static int OwnedCount(List<LicenseEntry> licenses)
        {
            int owned = 0;
            foreach (var entry in licenses)
                if (entry.Owned) owned++;
            return owned;
        }

        public static int UnlockAll()
        {
            if (!Guard()) return 0;

            int unlocked = 0;
            foreach (var entry in All())
            {
                if (entry.Owned) continue;
                if (Unlock(entry.Id)) unlocked++;
            }
            MelonLogger.Msg($"[CheatForDev] Unlocked {unlocked} licence(s).");
            return unlocked;
        }

        public static bool Unlock(int brandId)
        {
            if (!Guard()) return false;
            try
            {
                var service = GameAccess.Service<ProductsService>();
                if (service != null)
                {
                    service.BuyBrandSystem(brandId);
                }
                else
                {
                    var controller = GameAccess.Products_Controller;
                    if (controller == null) return false;
                    controller.BuyBrandSystem(brandId);
                }
                DevLog.Log($"[CheatForDev] Licence {brandId} unlocked.");
                return true;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[CheatForDev] Unlocking licence {brandId} failed: {ex}");
                return false;
            }
        }

        private static LicenseEntry Describe(PickupDefinition definition)
        {
            string name;
            try
            {
                name = definition.Name;
            }
            catch
            {
                name = null;
            }
            if (string.IsNullOrEmpty(name)) name = $"#{definition.Id}";

            return new LicenseEntry
            {
                Id = definition.Id,
                Label = name,
                Owned = IsOwned(definition.Id),
            };
        }

        private static bool IsOwned(int brandId)
        {
            try
            {
                var service = GameAccess.Service<ProductsService>();
                if (service != null) return service.HasBrandBought(brandId);

                var controller = GameAccess.Products_Controller;
                if (controller == null) return false;

                var brands = controller.Brands;
                return brands != null && brands.ContainsKey(brandId) && brands[brandId];
            }
            catch
            {
                return false;
            }
        }

        private static List<LicenseEntry> Sorted(List<LicenseEntry> entries)
        {
            entries.Sort((a, b) => string.CompareOrdinal(a.Label, b.Label));
            return entries;
        }

        private static bool Guard()
        {
            if (GameAccess.IsServer) return true;
            MelonLogger.Warning("[CheatForDev] Only the host can unlock licences.");
            return false;
        }
    }
}

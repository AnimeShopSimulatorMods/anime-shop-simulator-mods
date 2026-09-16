using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppProject.Code.Gameplay.Definitions;
using MelonLoader;

namespace CheatForDev.Cheats
{
    // Free deliveries. SpawnOrders drops boxes straight onto the delivery point without touching the
    // basket or the wallet, which is what "unlimited ordering" actually means here.
    internal static class OrderCheats
    {
        public const int MaxPerBurst = 200;

        internal sealed class BoxEntry
        {
            public int Id;
            public string Label;
        }

        private static List<BoxEntry> _catalog;

        public static void InvalidateCatalog() => _catalog = null;

        public static List<BoxEntry> Catalog()
        {
            if (_catalog != null) return _catalog;

            var list = new List<BoxEntry>();
            try
            {
                var products = GameAccess.Products;
                if (products == null) return list;

                var definitions = products._pickupDefinitions;
                if (definitions == null) return list;

                for (int i = 0; i < definitions.Count; i++)
                {
                    var definition = definitions[i];
                    if (definition == null) continue;
                    list.Add(new BoxEntry { Id = definition.Id, Label = Label(definition) });
                }
                list.Sort((a, b) => string.CompareOrdinal(a.Label, b.Label));
                _catalog = list;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[CheatForDev] Reading the box catalog failed: {ex.Message}");
            }
            return list;
        }

        public static void Spawn(int pickupDefinitionId, int count)
        {
            if (!Guard()) return;
            if (count <= 0) return;

            count = Math.Min(count, MaxPerBurst);
            try
            {
                var ids = new Il2CppStructArray<int>(count);
                for (int i = 0; i < count; i++) ids[i] = pickupDefinitionId;

                GameAccess.Orders.SpawnOrders(ids);
                Main.Log($"[CheatForDev] Spawned {count} box(es) of definition {pickupDefinitionId}.");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[CheatForDev] Spawning boxes failed: {ex}");
            }
        }

        private static string Label(PickupDefinition definition)
        {
            try
            {
                string name = definition.Name;
                if (string.IsNullOrEmpty(name)) name = $"#{definition.Id}";
                string suffix = definition.NotAvailable ? " (locked)" : string.Empty;
                return $"{name} [{definition.Type}]{suffix}";
            }
            catch
            {
                return $"#{definition.Id}";
            }
        }

        private static bool Guard()
        {
            if (GameAccess.Orders == null) return false;
            if (GameAccess.IsServer) return true;
            MelonLogger.Warning("[CheatForDev] Only the host can spawn orders.");
            return false;
        }
    }
}

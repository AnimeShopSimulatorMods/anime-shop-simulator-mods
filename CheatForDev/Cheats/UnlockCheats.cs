using System;
using Il2CppProject.Code.Gameplay.Configs;
using Il2CppProject.Code.Gameplay.Controllers;
using MelonLoader;

namespace CheatForDev.Cheats
{
    // Unlocks, upgrades and debt. Several of these only exist as methods on the game's own CheatController,
    // which may or may not still be in the shipped scene, so each one reports whether it is reachable.
    internal static class UnlockCheats
    {
        private static CheatController _native;

        public static void Reset() => _native = null;

        // The game's built-in cheat panel. Present it as optional: everything that needs it is disabled
        // in the menu when it is missing rather than throwing at the player.
        public static CheatController Native
        {
            get
            {
                if (_native != null) return _native;
                try
                {
                    _native = UnityEngine.Object.FindObjectOfType<CheatController>();
                    if (_native == null)
                    {
                        var all = UnityEngine.Resources.FindObjectsOfTypeAll<CheatController>();
                        if (all != null && all.Length > 0) _native = all[0];
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"[CheatForDev] Looking for the game's cheat controller failed: {ex.Message}");
                }
                return _native;
            }
        }

        public static bool HasNative => Native != null;

        public static void OpenAllCards()
        {
            if (!RequireNative()) return;
            try
            {
                Native.OpenAllCards();
                Main.Log("[CheatForDev] Opened every card.");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[CheatForDev] Opening cards failed: {ex}");
            }
        }

        public static void UnlockInventoryTools()
        {
            if (!RequireNative()) return;
            try
            {
                Native.UnlockAllInventoryToolsCheat();
                Main.Log("[CheatForDev] Unlocked every inventory tool.");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[CheatForDev] Unlocking inventory tools failed: {ex}");
            }
        }

        // The panel is a live object in the shipped scene, so the developers' own UI can simply be shown.
        public static void ToggleNativePanel()
        {
            if (!RequireNative()) return;
            try
            {
                Native.CanvasToggle();
                Main.Log("[CheatForDev] Toggled the game's own cheat panel.");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[CheatForDev] Toggling the game's cheat panel failed: {ex}");
            }
        }

        public static void SpawnBuildings()
        {
            if (!RequireNative()) return;
            try
            {
                Native.SpawnBuildings();
                Main.Log("[CheatForDev] Spawned the debug buildings.");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[CheatForDev] Spawning buildings failed: {ex}");
            }
        }

        public static int StoreLevel
        {
            get
            {
                try
                {
                    var controller = UnityEngine.Object.FindObjectOfType<UpgradeController>();
                    return controller == null ? 0 : controller.GetStoreLevel();
                }
                catch
                {
                    return 0;
                }
            }
        }

        public static void BuyNextUpgrade(UpgradeType type)
        {
            if (!GameAccess.IsServer)
            {
                MelonLogger.Warning("[CheatForDev] Only the host can buy upgrades.");
                return;
            }
            try
            {
                var controller = UnityEngine.Object.FindObjectOfType<UpgradeController>();
                if (controller == null) return;

                int index = controller.GetBoughtUpgradeCount(type);
                bool opened = controller.OpenUpgradeSystem(type, index);
                Main.Log($"[CheatForDev] Upgrade {type} step {index}: {(opened ? "unlocked" : "refused by the game")}.");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[CheatForDev] Buying the {type} upgrade failed: {ex}");
            }
        }

        public static float OutstandingBills
        {
            get
            {
                try
                {
                    var payments = UnityEngine.Object.FindObjectOfType<PaymentsController>();
                    return payments == null ? 0f : payments.GetOutstandingBillsAndRentAmount();
                }
                catch
                {
                    return 0f;
                }
            }
        }

        private static bool RequireNative()
        {
            if (HasNative) return true;
            MelonLogger.Warning("[CheatForDev] The game's own cheat controller is not in this build, so this action is unavailable.");
            return false;
        }
    }
}

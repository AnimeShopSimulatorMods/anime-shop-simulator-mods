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

        // The mop, the trash bag, the decor kit and the bat are each gated by a server SyncVar on
        // QuestController, which is what the save records and what the inventory bar reads through
        // QuestService.IsToolAvailable. Those four flags are the real unlock.
        //
        // The game's own UnlockAllInventoryToolsCheat() is a different thing: it flips one static bool on
        // CheatController that the availability check consults as an override. Nothing writes it to the
        // save, so it is gone the moment you quit - which is exactly what players reported. It is used
        // here only as a fallback when the quest controller is missing, and the log says when that
        // happens so nobody is told a session-only flag is permanent.
        public static void UnlockInventoryTools()
        {
            if (!GameAccess.IsServer)
            {
                MelonLogger.Warning("[CheatForDev] Only the host can unlock inventory tools.");
                return;
            }

            var quests = QuestControllerOrNull;
            if (quests == null)
            {
                MelonLogger.Warning("[CheatForDev] No QuestController in the scene, so the unlock cannot be saved. " +
                                    "Falling back to the game's session-only cheat flag; it will be gone after a restart.");
                ToggleNativeToolCheat();
                return;
            }

            MelonLogger.Msg("[CheatForDev] Unlocking inventory tools.");
            try
            {
                LogToolAvailability(quests, "before");

                int changed = 0;
                changed += SetUnlockFlag(quests, "mop", () => quests._isMopUnlocked.Value,
                    value => quests._isMopUnlocked.Value = value);
                changed += SetUnlockFlag(quests, "trash bag", () => quests._isTrashBagUnlocked.Value,
                    value => quests._isTrashBagUnlocked.Value = value);
                changed += SetUnlockFlag(quests, "decor kit", () => quests._isDecorUnlocked.Value,
                    value => quests._isDecorUnlocked.Value = value);
                changed += SetUnlockFlag(quests, "bat", () => quests._isBatUnlocked.Value,
                    value => quests._isBatUnlocked.Value = value);

                // The inventory bar is rebuilt from quest state rather than polled, so ask for a rebuild
                // instead of waiting for whatever would have changed next.
                try
                {
                    quests.RefreshPresentation();
                    Main.Log("[CheatForDev]   refreshed the inventory bar.");
                }
                catch (Exception ex)
                {
                    Main.Log($"[CheatForDev]   could not refresh the inventory bar ({ex.GetType().Name}); " +
                             "it will catch up on its own.");
                }

                try
                {
                    quests.Save();
                    Main.Log("[CheatForDev]   saved the quest state.");
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[CheatForDev]   saving the quest state failed: {ex.Message}. " +
                                        "It should still be written with the next ordinary save.");
                }

                LogToolAvailability(quests, "after");
                MelonLogger.Msg(changed > 0
                    ? $"[CheatForDev] Unlocked every inventory tool ({changed} flag(s) changed). This is saved."
                    : "[CheatForDev] Every inventory tool was already unlocked; nothing to change.");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[CheatForDev] Unlocking inventory tools failed: {ex}");
            }
        }

        // Drawn every frame, so it stays cheap and never throws into the middle of an OnGUI pass.
        public static string InventoryToolStatus
        {
            get
            {
                try
                {
                    var quests = QuestControllerOrNull;
                    if (quests == null) return "No quest controller in this scene, so tool state cannot be read.";

                    return $"mop {Word(quests._isMopUnlocked.Value)}, " +
                           $"trash bag {Word(quests._isTrashBagUnlocked.Value)}, " +
                           $"decor kit {Word(quests._isDecorUnlocked.Value)}, " +
                           $"bat {Word(quests._isBatUnlocked.Value)}.";
                }
                catch (Exception ex)
                {
                    return $"Cannot read the tool state ({ex.GetType().Name}).";
                }
            }
        }

        private static string Word(bool unlocked) => unlocked ? "unlocked" : "locked";

        // ------------------------------------------------------------- the crystal shop

        // The stand that sells special packs - boosters, pins, keychains - priced in crystals rather than
        // money. It is hidden in the world until a quest opens it, and that is one more SyncVar on
        // QuestController, saved in QuestSaveData.IsSpecialShopUnlocked. Setting it runs the game's own
        // visibility handler, so the stand appears without a reload.
        public static string SpecialShopStatus
        {
            get
            {
                try
                {
                    var quests = QuestControllerOrNull;
                    if (quests == null) return "No quest controller in this scene, so shop state cannot be read.";
                    return $"Crystal shop (special packs): {Word(quests._isSpecialShopUnlocked.Value)}.";
                }
                catch (Exception ex)
                {
                    return $"Cannot read the crystal shop state ({ex.GetType().Name}).";
                }
            }
        }

        public static void UnlockSpecialShop()
        {
            if (!GameAccess.IsServer)
            {
                MelonLogger.Warning("[CheatForDev] Only the host can open the crystal shop.");
                return;
            }

            var quests = QuestControllerOrNull;
            if (quests == null)
            {
                MelonLogger.Warning("[CheatForDev] Cannot open the crystal shop: no QuestController in the scene.");
                return;
            }

            MelonLogger.Msg("[CheatForDev] Opening the crystal shop.");
            try
            {
                int changed = SetUnlockFlag(quests, "crystal shop", () => quests._isSpecialShopUnlocked.Value,
                    value => quests._isSpecialShopUnlocked.Value = value);

                // IsSpecialShopUnlocked() is what the stand itself asks, so read it back rather than
                // trusting the flag we just wrote.
                try
                {
                    Main.Log($"[CheatForDev]   the game now reports the shop as " +
                             $"{Word(quests.IsSpecialShopUnlocked())}.");
                }
                catch (Exception ex)
                {
                    Main.Log($"[CheatForDev]   could not read the shop state back ({ex.GetType().Name}).");
                }

                try
                {
                    quests.Save();
                    Main.Log("[CheatForDev]   saved the quest state.");
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[CheatForDev]   saving the quest state failed: {ex.Message}. " +
                                        "It should still be written with the next ordinary save.");
                }

                MelonLogger.Msg(changed > 0
                    ? "[CheatForDev] The crystal shop is open. This is saved."
                    : "[CheatForDev] The crystal shop was already open; nothing to change.");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[CheatForDev] Opening the crystal shop failed: {ex}");
            }
        }

        public static QuestController QuestControllerOrNull
        {
            get
            {
                try
                {
                    return UnityEngine.Object.FindObjectOfType<QuestController>();
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"[CheatForDev] Looking for the quest controller failed: {ex.Message}");
                    return null;
                }
            }
        }

        private static int SetUnlockFlag(QuestController quests, string label, Func<bool> read, Action<bool> write)
        {
            try
            {
                if (read())
                {
                    Main.Log($"[CheatForDev]   {label}: already unlocked.");
                    return 0;
                }

                write(true);

                if (!read())
                {
                    MelonLogger.Warning($"[CheatForDev]   {label}: the flag did not stick. The game refused the write.");
                    return 0;
                }

                Main.Log($"[CheatForDev]   {label}: unlocked.");
                return 1;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[CheatForDev]   {label}: could not be unlocked ({ex.GetType().Name}: {ex.Message}).");
                return 0;
            }
        }

        // The menu item ids are small and contiguous, so a short sweep is enough to show in the log which
        // slots the game now considers available. Purely diagnostic.
        private static void LogToolAvailability(QuestController quests, string when)
        {
            if (!Main.VerboseLogs) return;
            try
            {
                var line = new System.Text.StringBuilder();
                for (int id = 0; id < 10; id++)
                {
                    bool available;
                    try { available = quests.IsToolAvailable(id); }
                    catch { break; }
                    line.Append(id).Append('=').Append(available ? "yes " : "no  ");
                }
                Main.Log($"[CheatForDev]   tool availability {when}: {line}");
            }
            catch
            {
                // Diagnostics must never take the action down with them.
            }
        }

        // Kept separate because it is a toggle, not a switch: calling it twice puts the tools back.
        public static void ToggleNativeToolCheat()
        {
            if (!RequireNative()) return;
            try
            {
                Native.UnlockAllInventoryToolsCheat();
                MelonLogger.Msg("[CheatForDev] Toggled the game's session-only inventory tool cheat. " +
                                "It is not written to the save.");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[CheatForDev] Toggling the inventory tool cheat failed: {ex}");
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

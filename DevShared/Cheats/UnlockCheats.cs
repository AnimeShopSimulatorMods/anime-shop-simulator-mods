using System;
using System.Collections.Generic;
using Il2CppProject.Code.Gameplay.Configs;
using Il2CppProject.Code.Gameplay.Controllers;
using MelonLoader;

namespace AnimeShopMods.Dev.Cheats
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
                DevLog.Log("[CheatForDev] Opened every card.");
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
                    DevLog.Log("[CheatForDev]   refreshed the inventory bar.");
                }
                catch (Exception ex)
                {
                    DevLog.Log($"[CheatForDev]   could not refresh the inventory bar ({ex.GetType().Name}); " +
                             "it will catch up on its own.");
                }

                try
                {
                    quests.Save();
                    DevLog.Log("[CheatForDev]   saved the quest state.");
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

        private static string OpenWord(bool open) => open ? "open" : "closed";

        // ------------------------------------------------------------- the clothes shop

        // The outfit stand is not a flag but a date: QuestSaveData.OutfitOpenerUnlockDay, held live in
        // the _outfitOpenerUnlockDay SyncVar, and the game asks IsOutfitOpenerOpen(currentDay). Setting
        // the unlock day to today is therefore all it takes, and asking the game the same question back
        // is the only honest way to know it worked without guessing how the comparison is written.
        public static string OutfitShopStatus
        {
            get
            {
                try
                {
                    var quests = QuestControllerOrNull;
                    if (quests == null) return "No quest controller in this scene, so shop state cannot be read.";

                    int questDay = quests.GetCurrentDay();
                    return $"Clothes shop: {OpenWord(quests.IsOutfitOpenerOpen(questDay))} (opens on day " +
                           $"{quests._outfitOpenerUnlockDay.Value}, quest day is {questDay}).";
                }
                catch (Exception ex)
                {
                    return $"Cannot read the clothes shop state ({ex.GetType().Name}).";
                }
            }
        }

        public static void OpenOutfitShop()
        {
            if (!GameAccess.IsServer)
            {
                MelonLogger.Warning("[CheatForDev] Only the host can open the clothes shop.");
                return;
            }

            var quests = QuestControllerOrNull;
            if (quests == null)
            {
                MelonLogger.Warning("[CheatForDev] Cannot open the clothes shop: no QuestController in the scene.");
                return;
            }

            MelonLogger.Msg("[CheatForDev] Opening the clothes shop.");
            try
            {
                int before = quests._outfitOpenerUnlockDay.Value;
                int today = quests.GetCurrentDay();
                MelonLogger.Msg($"  before: opens on day {before}, quest day {today}, clock day {ClockDay()}, " +
                                $"currently {OpenWord(quests.IsOutfitOpenerOpen(today))}.");

                if (quests.IsOutfitOpenerOpen(today))
                {
                    MelonLogger.Msg("[CheatForDev] The clothes shop was already open; nothing to change.");
                    return;
                }

                // The shop opens from day one onwards, so on a save whose day counter is still zero there
                // is no unlock day that can satisfy it. The counter being stuck at zero is the real fault
                // - the next-day button used to leave it there - so move it on rather than reporting a
                // failure the player cannot act on.
                if (today < 1)
                {
                    MelonLogger.Msg("[CheatForDev]   the day counter is 0, which no unlock day can satisfy; " +
                                    "moving it to day 1.");
                    ProgressCheats.Set(ParameterType.Day, 1f);
                    today = quests.GetCurrentDay();
                    MelonLogger.Msg($"[CheatForDev]   the quest system now reports day {today}.");

                    if (today < 1)
                    {
                        MelonLogger.Warning("[CheatForDev] The quest system still reports day 0 after raising " +
                                            "the day counter, so it reads the day from somewhere else. " +
                                            "Sweeping the condition; please send this block to the mod author.");
                        SweepOutfitCondition(quests, before, today);
                        quests._outfitOpenerUnlockDay.Value = before;
                        return;
                    }
                }

                // The quest system and the clock disagree about what day it is - GetCurrentDay() answered 0
                // on a save where TimeController said day 1 - so try every day either of them might mean
                // rather than picking one and hoping. Ordered cheapest-first: the lowest unlock day that
                // satisfies a "must be at least 1" rule, then today, then the clock's idea of today.
                foreach (int candidate in CandidateUnlockDays(quests, today))
                {
                    if (!TrySetOutfitUnlockDay(quests, candidate, today)) continue;

                    RefreshAndSave(quests);
                    MelonLogger.Msg($"[CheatForDev] The clothes shop is open (unlock day {candidate}). This is saved.");
                    return;
                }

                MelonLogger.Warning("[CheatForDev] The clothes shop is still closed whatever the unlock day is " +
                                    "set to, so the game gates it on something else as well. Sweeping the " +
                                    "condition now; please send this block to the mod author.");
                SweepOutfitCondition(quests, before, today);
                quests._outfitOpenerUnlockDay.Value = before;
                MelonLogger.Msg($"[CheatForDev] Put the unlock day back to {before}; nothing was saved.");
                return;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[CheatForDev] Opening the clothes shop failed: {ex}");
            }
        }

        // The quest controller's day and the clock's day are not the same number, so collect both and
        // work from the smallest sensible unlock day upwards. Distinct, order preserved.
        private static List<int> CandidateUnlockDays(QuestController quests, int questDay)
        {
            var days = new List<int>();
            void Add(int value)
            {
                if (value >= 0 && !days.Contains(value)) days.Add(value);
            }

            Add(1);
            Add(questDay);
            Add(ClockDay());
            Add(0);
            return days;
        }

        private static int ClockDay()
        {
            try
            {
                var time = GameAccess.Time;
                return time == null ? -1 : time.GetCurrentTime().Day;
            }
            catch
            {
                return -1;
            }
        }

        // When nothing opens the shop, stop guessing and print the truth table instead: every unlock day
        // against every day the game might be asking about. One run of this says exactly how the
        // condition is written, which is cheaper than another round of changing a number and relaunching.
        private static void SweepOutfitCondition(QuestController quests, int originalUnlockDay, int questDay)
        {
            MelonLogger.Msg("===== [CheatForDev] clothes shop sweep =====");
            MelonLogger.Msg($"  QuestController.GetCurrentDay() = {questDay}");
            MelonLogger.Msg($"  TimeController day              = {ClockDay()}");
            MelonLogger.Msg($"  ParameterType.Day               = {ProgressCheats.Get(ParameterType.Day)}");
            MelonLogger.Msg($"  unlock day was                  = {originalUnlockDay}");
            MelonLogger.Msg("  IsOutfitOpenerOpen(asked day) for each unlock day:");

            int[] unlockDays = { -1, 0, 1, 2, 3, 5, 10 };
            int[] askedDays = { 0, 1, 2, 3, 5, 10, 999 };

            var header = new System.Text.StringBuilder("    unlock \\ asked ");
            foreach (int asked in askedDays) header.Append(asked.ToString().PadLeft(6));
            MelonLogger.Msg(header.ToString());

            foreach (int unlockDay in unlockDays)
            {
                var row = new System.Text.StringBuilder($"    {unlockDay,14} ");
                foreach (int asked in askedDays)
                {
                    string cell;
                    try
                    {
                        quests._outfitOpenerUnlockDay.Value = unlockDay;
                        cell = quests.IsOutfitOpenerOpen(asked) ? "open" : "-";
                    }
                    catch (Exception ex)
                    {
                        cell = ex.GetType().Name;
                    }
                    row.Append(cell.PadLeft(6));
                }
                MelonLogger.Msg(row.ToString());
            }

            MelonLogger.Msg("===== [CheatForDev] sweep end =====");
        }

        private static bool TrySetOutfitUnlockDay(QuestController quests, int unlockDay, int questDay)
        {
            try
            {
                quests._outfitOpenerUnlockDay.Value = unlockDay;

                // Only the quest day counts. Testing against the clock day as well once reported the shop
                // open while it was still shut in the world, because the game's own call site passes the
                // quest day - the clock's idea of the date is a different number entirely.
                bool open = quests.IsOutfitOpenerOpen(questDay);
                DevLog.Log($"[CheatForDev]   unlock day {unlockDay}, asked with quest day {questDay} -> " +
                         $"{OpenWord(open)}.");
                return open;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[CheatForDev]   setting the unlock day to {unlockDay} failed " +
                                    $"({ex.GetType().Name}: {ex.Message}).");
                return false;
            }
        }

        // -------------------------------------------------------------- the second floor

        // The upstairs room is not a flag either: it opens once the quest scenario that grants it has
        // been passed, so what has to move is QuestSaveData.ScenarioIndex. The scenario is found by
        // asking the game which one it is rather than hard-coding a number, because that number would
        // quietly become wrong the first time the developers insert a quest ahead of it.
        public static string SecondFloorStatus
        {
            get
            {
                try
                {
                    var quests = QuestControllerOrNull;
                    if (quests == null) return "No quest controller in this scene, so floor state cannot be read.";

                    bool done = quests.HasCompletedSecondFloorQuest();
                    return $"Second floor: {OpenWord(done)} (quest progress {quests._scenarioIndex.Value} " +
                           $"of {quests.GetScenarioCount()}).";
                }
                catch (Exception ex)
                {
                    return $"Cannot read the second floor state ({ex.GetType().Name}).";
                }
            }
        }

        public static void OpenSecondFloor()
        {
            if (!GameAccess.IsServer)
            {
                MelonLogger.Warning("[CheatForDev] Only the host can open the second floor.");
                return;
            }

            var quests = QuestControllerOrNull;
            if (quests == null)
            {
                MelonLogger.Warning("[CheatForDev] Cannot open the second floor: no QuestController in the scene.");
                return;
            }

            MelonLogger.Msg("[CheatForDev] Opening the second floor.");
            try
            {
                if (quests.HasCompletedSecondFloorQuest())
                {
                    MelonLogger.Msg("[CheatForDev] The second floor was already open; nothing to change.");
                    return;
                }

                int before = quests._scenarioIndex.Value;
                int index = FindSecondFloorScenarioIndex(quests);
                MelonLogger.Msg($"  before: quest progress {before}, second-floor quest is " +
                                $"{(index < 0 ? "not in the config" : $"number {index}")}.");

                if (index < 0)
                {
                    MelonLogger.Warning("[CheatForDev] Could not find the second-floor quest in this build, " +
                                        "so there is nothing safe to move the progress to.");
                    return;
                }

                if (before <= index)
                {
                    quests._scenarioIndex.Value = index + 1;
                    DevLog.Log($"[CheatForDev]   quest progress moved from {before} to {index + 1}.");
                }

                try
                {
                    quests.ApplySecondFloorQuestVisibility();
                    DevLog.Log("[CheatForDev]   applied the second floor's visibility.");
                }
                catch (Exception ex)
                {
                    DevLog.Log($"[CheatForDev]   could not apply the visibility ({ex.GetType().Name}); " +
                             "it should catch up on the next load.");
                }

                if (!quests.HasCompletedSecondFloorQuest())
                {
                    MelonLogger.Warning("[CheatForDev] The game still reports the second-floor quest as " +
                                        "unfinished. Nothing was saved for it.");
                    return;
                }

                RefreshAndSave(quests);
                MelonLogger.Msg("[CheatForDev] The second floor is open. This is saved.");
                MelonLogger.Warning("[CheatForDev] Note: quest progress was moved forward to get here, so any " +
                                    "quest before the second-floor one now counts as done.");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[CheatForDev] Opening the second floor failed: {ex}");
            }
        }

        private static int FindSecondFloorScenarioIndex(QuestController quests)
        {
            try
            {
                var config = quests._config;
                if (config == null || config._scenarios == null) return -1;

                for (int i = 0; i < config._scenarios.Length; i++)
                {
                    var scenario = config.GetScenario(i);
                    if (scenario != null && QuestController.IsSecondFloorScenario(scenario)) return i;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[CheatForDev]   could not search the quest list ({ex.GetType().Name}: {ex.Message}).");
            }
            return -1;
        }

        // ------------------------------------------------------------------------ shared

        private static void RefreshAndSave(QuestController quests)
        {
            try
            {
                quests.NotifyStateChanged();
                quests.RefreshPresentation();
                DevLog.Log("[CheatForDev]   refreshed the quest presentation.");
            }
            catch (Exception ex)
            {
                DevLog.Log($"[CheatForDev]   could not refresh the presentation ({ex.GetType().Name}); " +
                         "it will catch up on its own.");
            }

            try
            {
                quests.Save();
                DevLog.Log("[CheatForDev]   saved the quest state.");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[CheatForDev]   saving the quest state failed: {ex.Message}. " +
                                    "It should still be written with the next ordinary save.");
            }
        }

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
                    DevLog.Log($"[CheatForDev]   the game now reports the shop as " +
                             $"{Word(quests.IsSpecialShopUnlocked())}.");
                }
                catch (Exception ex)
                {
                    DevLog.Log($"[CheatForDev]   could not read the shop state back ({ex.GetType().Name}).");
                }

                try
                {
                    quests.Save();
                    DevLog.Log("[CheatForDev]   saved the quest state.");
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
                    DevLog.Log($"[CheatForDev]   {label}: already unlocked.");
                    return 0;
                }

                write(true);

                if (!read())
                {
                    MelonLogger.Warning($"[CheatForDev]   {label}: the flag did not stick. The game refused the write.");
                    return 0;
                }

                DevLog.Log($"[CheatForDev]   {label}: unlocked.");
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
            if (!DevLog.Verbose) return;
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
                DevLog.Log($"[CheatForDev]   tool availability {when}: {line}");
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
                DevLog.Log("[CheatForDev] Toggled the game's own cheat panel.");
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
                DevLog.Log("[CheatForDev] Spawned the debug buildings.");
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
                DevLog.Log($"[CheatForDev] Upgrade {type} step {index}: {(opened ? "unlocked" : "refused by the game")}.");
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

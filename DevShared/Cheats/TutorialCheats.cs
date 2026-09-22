using System;
using Il2CppProject.Code.Gameplay.Configs;
using Il2CppProject.Code.Gameplay.Controllers;
using MelonLoader;
using UnityEngine;

namespace AnimeShopMods.Dev.Cheats
{
    // Skipping the tutorial means finishing two separate systems, both owned by TutorController:
    //
    //   - the step tutorial, a single server SyncVar holding (step, count) that drives the task panel and
    //     gates the open-shop sign until the player has done what each step asks;
    //   - the UI tutorials, the one-off popups, tracked per player as a set of completed indexes.
    //
    // Finishing only the first leaves popups firing at you for the rest of the save, so both are done
    // together. The step is moved straight to one past the last one in the config rather than replaying
    // every step in turn: replaying runs each step's completion logic, which spawns and grants things,
    // and doing that twenty times in one frame is a far better way to break a save than to skip a tutorial.
    internal static class TutorialCheats
    {
        private static TutorController _controller;

        public static void Reset() => _controller = null;

        public static TutorController Controller
        {
            get
            {
                if (_controller != null) return _controller;
                try
                {
                    _controller = UnityEngine.Object.FindObjectOfType<TutorController>();
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"[CheatForDev] Looking for the tutorial controller failed: {ex.Message}");
                }
                return _controller;
            }
        }

        public static bool Available => Controller != null;

        // Drawn every frame, so it stays cheap and never throws into the middle of an OnGUI pass.
        public static string Status
        {
            get
            {
                try
                {
                    var controller = Controller;
                    if (controller == null) return "The tutorial controller is not in this scene.";

                    var state = controller._sharedTutorialState.Value;
                    int total = StepCount(controller);
                    int done = controller._localUiCompleted == null ? 0 : controller._localUiCompleted.Count;
                    int popups = PopupCount(controller);

                    string step = total > 0
                        ? $"step {state.x} of {total}"
                        : $"step {state.x}";
                    string ui = popups > 0
                        ? $"{done} of {popups} popups seen"
                        : $"{done} popups seen";

                    return IsFinished(controller) ? $"Finished ({step}, {ui})." : $"In progress: {step}, {ui}.";
                }
                catch (Exception ex)
                {
                    return $"Cannot read the tutorial state ({ex.GetType().Name}).";
                }
            }
        }

        public static bool IsFinished(TutorController controller)
        {
            int total = StepCount(controller);
            if (total <= 0) return false;
            return controller._sharedTutorialState.Value.x >= total;
        }

        public static void SkipAll()
        {
            if (!GameAccess.IsServer)
            {
                MelonLogger.Warning("[CheatForDev] Only the host can skip the tutorial.");
                return;
            }

            var controller = Controller;
            if (controller == null)
            {
                MelonLogger.Warning("[CheatForDev] Cannot skip the tutorial: no TutorController in the scene.");
                return;
            }

            MelonLogger.Msg("[CheatForDev] Skipping the tutorial.");
            try
            {
                var before = controller._sharedTutorialState.Value;
                int total = StepCount(controller);
                MelonLogger.Msg($"  before: step {before.x}, count {before.y}, {total} steps in the config.");

                SkipSteps(controller, total);
                SkipPopups(controller);

                // Everything the tutorial opens up on the way through is gated by the quest controller
                // rather than by the tutorial itself: the tools, the crystal shop, the clothes shop and
                // the second floor. Skipping past the steps that would have granted them leaves a player
                // who has "finished" the tutorial with no mop, nowhere to spend crystals and a locked
                // upstairs, so grant the lot here.
                UnlockCheats.UnlockInventoryTools();
                UnlockCheats.UnlockSpecialShop();
                UnlockCheats.OpenOutfitShop();
                UnlockCheats.OpenSecondFloor();

                // The tutorial writes into the save through its own controller, so persist it here rather
                // than leaving the skip to be undone by the next load.
                try
                {
                    controller.Save();
                    DevLog.Log("[CheatForDev]   saved the tutorial state.");
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[CheatForDev]   saving the tutorial state failed: {ex.Message}. " +
                                        "It should still be written with the next ordinary save.");
                }

                var after = controller._sharedTutorialState.Value;
                MelonLogger.Msg($"  after:  step {after.x}, count {after.y}.");

                if (after.x == before.x && total > 0 && before.x < total)
                    MelonLogger.Warning("[CheatForDev] The tutorial step did not move. The game refused the " +
                                        "write, or this build keeps the step somewhere else.");
                else
                    MelonLogger.Msg("[CheatForDev] Tutorial skipped.");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[CheatForDev] Skipping the tutorial failed: {ex}");
            }
        }

        // ---------------------------------------------------------------------------- internals

        // One past the last step is the config's own "nothing left to show" value: GetActiveData returns
        // null there, so the task panel hides itself and the open-shop sign unlocks.
        private static void SkipSteps(TutorController controller, int total)
        {
            if (total <= 0)
            {
                MelonLogger.Warning("[CheatForDev]   no tutorial steps in the config; leaving the step alone.");
                return;
            }

            try
            {
                controller._sharedTutorialState.Value = new Vector2Int(total, 0);
                DevLog.Log($"[CheatForDev]   moved the step to {total} (one past the last).");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[CheatForDev]   setting the tutorial step failed: {ex.Message}");
                return;
            }

            // The SyncVar reaches other players on its own, but the host's own panel is built from the
            // applied state, so nudge it rather than waiting for the next change callback.
            try
            {
                controller.ApplySharedTutorialState(total, 0, false);
                DevLog.Log("[CheatForDev]   refreshed the local tutorial panel.");
            }
            catch (Exception ex)
            {
                DevLog.Log($"[CheatForDev]   could not refresh the panel ({ex.GetType().Name}); " +
                         "it will catch up on the next state change.");
            }
        }

        // CheckUITutorComplete is the game's own "this popup is done" entry, so it updates the local set,
        // the pending set and the synced dictionary the same way finishing the popup by hand would.
        private static void SkipPopups(TutorController controller)
        {
            try
            {
                controller.ClearUiTutorQueue();
                DevLog.Log("[CheatForDev]   cleared the popup queue.");
            }
            catch (Exception ex)
            {
                DevLog.Log($"[CheatForDev]   could not clear the popup queue ({ex.GetType().Name}).");
            }

            var config = controller._tutorialConfig;
            if (config == null || config._tutorialData == null)
            {
                MelonLogger.Warning("[CheatForDev]   no UI tutorial list in the config; popups were left alone.");
                return;
            }

            int marked = 0;
            int failed = 0;
            foreach (var data in config._tutorialData)
            {
                if (data == null || string.IsNullOrEmpty(data.Id)) continue;
                try
                {
                    controller.CheckUITutorComplete(data.Id);
                    marked++;
                }
                catch (Exception ex)
                {
                    failed++;
                    DevLog.Log($"[CheatForDev]   popup '{data.Id}' would not mark complete ({ex.GetType().Name}).");
                }
            }

            if (failed > 0)
                MelonLogger.Warning($"[CheatForDev]   marked {marked} popups complete, {failed} refused.");
            else
                DevLog.Log($"[CheatForDev]   marked {marked} popups complete.");
        }

        private static int StepCount(TutorController controller)
        {
            try
            {
                var config = controller._tutorialConfig;
                if (config == null || config._datas == null) return 0;
                return config._datas.Length;
            }
            catch
            {
                return 0;
            }
        }

        private static int PopupCount(TutorController controller)
        {
            try
            {
                var config = controller._tutorialConfig;
                if (config == null || config._tutorialData == null) return 0;
                return config._tutorialData.Length;
            }
            catch
            {
                return 0;
            }
        }
    }
}

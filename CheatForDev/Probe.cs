using System;
using Il2CppProject.Code.Core.Services;
using Il2CppProject.Code.Core.UI;
using Il2CppProject.Code.Gameplay.Configs;
using Il2CppProject.Code.Gameplay.Controllers;
using MelonLoader;
using UnityEngine;

namespace CheatForDev
{
    // One-off runtime survey, written before the menu so the rest of the mod is built on measured facts
    // instead of guesses. It answers three questions that change how everything else has to be written:
    //   1. Is the game's own CheatController still present in the shipped scene?
    //   2. Can AllServices.Get<T>() be called through Il2CppInterop, or must controllers be found by scene search?
    //   3. What does TimeController._serverMinutes count from, and what are the shop's opening hours?
    internal static class Probe
    {
        private static bool _done;
        private static float _nextAttempt;

        public static void Reset()
        {
            _done = false;
            _nextAttempt = 0f;
        }

        public static void Tick()
        {
            if (_done) return;
            if (Time.time < _nextAttempt) return;
            _nextAttempt = Time.time + 2f;

            var timeController = UnityEngine.Object.FindObjectOfType<TimeController>();
            if (timeController == null) return;   // world not up yet

            _done = true;
            MelonLogger.Msg("===== [CheatForDev] runtime probe =====");
            ProbeCheatController();
            ProbeServices();
            ProbeTime(timeController);
            ProbeControllers();
            MelonLogger.Msg("===== [CheatForDev] probe end =====");
        }

        private static void ProbeCheatController()
        {
            try
            {
                var active = UnityEngine.Object.FindObjectsOfType<CheatController>();
                var all = Resources.FindObjectsOfTypeAll<CheatController>();
                MelonLogger.Msg($"CheatController: {active.Length} active, {all.Length} total (incl. inactive/prefabs).");
                foreach (var controller in all)
                {
                    if (controller == null) continue;
                    var go = controller.gameObject;
                    MelonLogger.Msg($"  -> '{go.name}' activeInHierarchy={go.activeInHierarchy} scene='{go.scene.name}'");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"CheatController probe failed: {ex.Message}");
            }
        }

        private static void ProbeServices()
        {
            TryService<ParametersService>("ParametersService");
            TryService<UIService>("UIService");
        }

        private static void TryService<T>(string label) where T : Il2CppSystem.Object
        {
            try
            {
                var service = AllServices.Get<T>();
                MelonLogger.Msg($"AllServices.Get<{label}>() -> {(service == null ? "null" : "ok")}");
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"AllServices.Get<{label}>() -> THREW {ex.GetType().Name}: {ex.Message}");
            }
        }

        private static void ProbeTime(TimeController timeController)
        {
            try
            {
                var now = timeController.GetCurrentTime();
                MelonLogger.Msg($"TimeController.GetCurrentTime() -> {now.Hour:00}:{now.Minute:00} (day {now.Day})");
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"TimeController.GetCurrentTime() -> THREW {ex.Message}");
            }

            try
            {
                MelonLogger.Msg($"TimeController._serverMinutes = {timeController._serverMinutes.Value}");
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"TimeController._serverMinutes -> THREW {ex.Message}");
            }

            try
            {
                MelonLogger.Msg($"TimeController._updateTime = {timeController._updateTime.Value}, " +
                                $"IsTimeUpdating = {timeController.IsTimeUpdating}, " +
                                $"DebugTimeMultiplier = {timeController.DebugTimeMultiplier}");
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"TimeController state -> THREW {ex.Message}");
            }

            try
            {
                var config = timeController._timeConfig;
                if (config == null)
                    MelonLogger.Msg("TimeConfig -> null on the controller.");
                else
                    MelonLogger.Msg($"TimeConfig: StartHour={config.StartHour}, EndHour={config.EndHour}, " +
                                    $"UpdateInterval={config.UpdateInterval}, TimeSpeed={config.GetTimeSpeed()}");
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"TimeConfig -> THREW {ex.Message}");
            }
        }

        private static void ProbeControllers()
        {
            Report<ParametersController>("ParametersController");
            Report<OrderController>("OrderController");
            Report<EmployeesController>("EmployeesController");
            Report<BuyersController>("BuyersController");

            try
            {
                var parameters = UnityEngine.Object.FindObjectOfType<ParametersController>();
                if (parameters == null) return;
                foreach (ParameterType type in Enum.GetValues(typeof(ParameterType)))
                    MelonLogger.Msg($"  {type} = {parameters.GetFloatValue(type)}");
                MelonLogger.Msg($"  NeedExp() = {parameters.NeedExp()}");
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"Parameter values -> THREW {ex.Message}");
            }
        }

        private static void Report<T>(string label) where T : Component
        {
            try
            {
                var found = UnityEngine.Object.FindObjectOfType<T>();
                MelonLogger.Msg($"{label}: {(found == null ? "not in scene" : "found")}");
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"{label} -> THREW {ex.Message}");
            }
        }
    }
}

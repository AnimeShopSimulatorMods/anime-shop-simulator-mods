using System;
using Il2CppProject.Code.Core.Services;
using Il2CppProject.Code.Gameplay.Controllers;
using MelonLoader;
using UnityEngine;

namespace CheatForDev.Cheats
{
    // Money, shop experience and level, crystals, day counter and tournament wins all live in the same
    // ParameterType table, so one pair of read/write helpers covers the whole tab.
    internal static class ProgressCheats
    {
        private const float TopUpInterval = 0.5f;

        public static bool InfiniteMoney;
        public static float InfiniteMoneyTarget = 1_000_000f;

        private static float _nextTopUp;

        public static void Tick()
        {
            if (!InfiniteMoney || !GameAccess.IsServer) return;
            if (Time.time < _nextTopUp) return;
            _nextTopUp = Time.time + TopUpInterval;

            // Half the target is the trigger point, so ordinary spending still shows up in the UI
            // instead of the number snapping back on the same frame it changed.
            if (Get(ParameterType.Money) < InfiniteMoneyTarget * 0.5f)
                Set(ParameterType.Money, InfiniteMoneyTarget);
        }

        public static float Get(ParameterType type)
        {
            try
            {
                var controller = GameAccess.Parameters;
                return controller == null ? 0f : controller.GetFloatValue(type);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[CheatForDev] Reading {type} failed: {ex.Message}");
                return 0f;
            }
        }

        // The game's own NeedExp() throws until its experience table is ready, and the menu asks for this
        // every frame, so one failure is enough to stop asking.
        private static bool _needExpUnavailable;

        public static int NeedExp()
        {
            if (_needExpUnavailable) return -1;
            try
            {
                var controller = GameAccess.Parameters;
                return controller == null ? -1 : controller.NeedExp();
            }
            catch
            {
                _needExpUnavailable = true;
                return -1;
            }
        }

        public static void Set(ParameterType type, float value)
        {
            if (!Guard()) return;
            try
            {
                var service = GameAccess.Service<ParametersService>();
                if (service != null)
                {
                    service.SetParameter(type, value);
                }
                else
                {
                    var controller = GameAccess.Parameters;
                    if (controller == null) return;
                    controller.ApplyParameterValue(type, value);
                }
                Main.Log($"[CheatForDev] {type} set to {value}.");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[CheatForDev] Setting {type} to {value} failed: {ex}");
            }
        }

        public static void Add(ParameterType type, float delta)
        {
            if (!Guard()) return;
            try
            {
                var service = GameAccess.Service<ParametersService>();
                if (service != null)
                {
                    service.SumParameter(type, delta);
                }
                else
                {
                    var controller = GameAccess.Parameters;
                    if (controller == null) return;
                    controller.ApplyParameterDelta(type, delta, true);
                }
                Main.Log($"[CheatForDev] {type} changed by {delta}.");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[CheatForDev] Changing {type} by {delta} failed: {ex}");
            }
        }

        private static bool Guard()
        {
            if (GameAccess.IsServer) return true;
            MelonLogger.Warning("[CheatForDev] Only the host can change parameters.");
            return false;
        }
    }
}

using System;
using Il2CppFishNet.Connection;
using Il2CppProject.Code.Core.Services;
using Il2CppProject.Code.Gameplay.Controllers;
using MelonLoader;
using UnityEngine;

namespace AnimeShopMods.Dev.Cheats
{
    // Money, shop experience, level and the day counter share one table on the controller, keyed by
    // ParameterType, so a single pair of read/write helpers covers all four.
    //
    // Crystals and tournament wins do NOT live there, even though ParameterType has a Crystal and a Wins
    // member. The game keeps one balance per player, in _crystals and _wins, keyed by player id, and
    // reaches them through their own methods: SumCrystalForConnection / GetCrystal and SetWins / GetWins.
    // The two enum members exist only as change-notification tags, to tell the HUD which widget to
    // refresh. Routing them through SetParameter writes a number that nothing ever reads back, which is
    // what made both sections of the menu look dead. Guard() is not enough to catch that, so the generic
    // helpers refuse those two types outright and point at the per-player ones.
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

        // ------------------------------------------------------- the shared parameter table

        public static float Get(ParameterType type)
        {
            if (IsPerPlayer(type))
            {
                // Reading the table for these returns whatever junk a previous version left behind, so
                // answer from the store the game actually uses.
                return type == ParameterType.Crystal ? GetCrystals() : GetWins();
            }

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
            if (!Guard() || !GuardSharedTable(type, "set")) return;

            float before = Get(type);
            try
            {
                var service = GameAccess.Service<ParametersService>();
                if (service != null)
                {
                    service.SetParameter(type, value);
                    ReportWrite(type.ToString(), "ParametersService.SetParameter", value, before, Get(type));
                }
                else
                {
                    var controller = GameAccess.Parameters;
                    if (controller == null)
                    {
                        MelonLogger.Warning($"[CheatForDev] Cannot set {type}: no ParametersController in the scene.");
                        return;
                    }
                    controller.ApplyParameterValue(type, value);
                    ReportWrite(type.ToString(), "ParametersController.ApplyParameterValue", value, before, Get(type));
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[CheatForDev] Setting {type} to {value} failed: {ex}");
            }
        }

        public static void Add(ParameterType type, float delta)
        {
            if (!Guard() || !GuardSharedTable(type, "change")) return;

            float before = Get(type);
            try
            {
                var service = GameAccess.Service<ParametersService>();
                if (service != null)
                {
                    service.SumParameter(type, delta);
                    ReportWrite(type.ToString(), "ParametersService.SumParameter", before + delta, before, Get(type));
                }
                else
                {
                    var controller = GameAccess.Parameters;
                    if (controller == null)
                    {
                        MelonLogger.Warning($"[CheatForDev] Cannot change {type}: no ParametersController in the scene.");
                        return;
                    }
                    controller.ApplyParameterDelta(type, delta, true);
                    ReportWrite(type.ToString(), "ParametersController.ApplyParameterDelta", before + delta, before, Get(type));
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[CheatForDev] Changing {type} by {delta} failed: {ex}");
            }
        }

        // ------------------------------------------------------------------------ crystals

        public static int GetCrystals()
        {
            try
            {
                var connection = LocalConnection;
                if (connection == null) return 0;

                var service = GameAccess.Service<ParametersService>();
                if (service != null) return service.GetCrystal(connection);

                var controller = GameAccess.Parameters;
                return controller == null ? 0 : controller.GetCrystal(connection);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[CheatForDev] Reading crystals failed: {ex.Message}");
                return 0;
            }
        }

        // The game only ever adds to a crystal balance, so an absolute value becomes a delta here.
        public static void SetCrystals(float value)
        {
            AddCrystals(Mathf.Max(0f, value) - GetCrystals());
        }

        public static void AddCrystals(float delta)
        {
            if (!Guard()) return;
            if (Mathf.Approximately(delta, 0f)) return;

            int before = GetCrystals();
            try
            {
                var connection = LocalConnection;
                var service = GameAccess.Service<ParametersService>();

                // SumCrystalForConnection is the server-side entry and lands immediately; SumCrystal goes
                // out as a server RPC and only comes back a tick later. Prefer the direct one when the
                // connection is known, which on a host it always is.
                string route;
                if (service != null)
                {
                    if (connection != null)
                    {
                        service.SumCrystalForConnection(delta, connection);
                        route = "ParametersService.SumCrystalForConnection";
                    }
                    else
                    {
                        service.SumCrystal(delta);
                        route = "ParametersService.SumCrystal";
                    }
                }
                else
                {
                    var controller = GameAccess.Parameters;
                    if (controller == null)
                    {
                        MelonLogger.Warning("[CheatForDev] Cannot change crystals: no ParametersController in the scene.");
                        return;
                    }
                    if (connection != null)
                    {
                        controller.SumCrystalForConnection(delta, connection);
                        route = "ParametersController.SumCrystalForConnection";
                    }
                    else
                    {
                        controller.SumCrystal(delta);
                        route = "ParametersController.SumCrystal";
                    }
                }

                ReportWrite("Crystals", route, before + delta, before, GetCrystals());
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[CheatForDev] Changing crystals by {delta} failed: {ex}");
            }
        }

        // ------------------------------------------------------------------ tournament wins

        // -1 means the local player's id could not be resolved, which is different from a real zero and
        // the menu says so rather than showing a number it made up.
        public static int GetWins()
        {
            try
            {
                if (!TryGetLocalPlayerId(out ulong playerId)) return -1;

                var service = GameAccess.Service<ParametersService>();
                if (service != null) return service.GetWins(playerId);

                var controller = GameAccess.Parameters;
                return controller == null ? -1 : controller.GetWins(playerId);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[CheatForDev] Reading tournament wins failed: {ex.Message}");
                return -1;
            }
        }

        public static void SetWins(float value)
        {
            if (!Guard()) return;

            value = Mathf.Max(0f, value);
            int before = GetWins();
            try
            {
                var service = GameAccess.Service<ParametersService>();
                if (service != null)
                {
                    service.SetWins(value);
                    ReportWrite("Tournament wins", "ParametersService.SetWins", value, before, GetWins());
                }
                else
                {
                    var controller = GameAccess.Parameters;
                    if (controller == null)
                    {
                        MelonLogger.Warning("[CheatForDev] Cannot set tournament wins: no ParametersController in the scene.");
                        return;
                    }
                    controller.SetWins(value);
                    ReportWrite("Tournament wins", "ParametersController.SetWins", value, before, GetWins());
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[CheatForDev] Setting tournament wins to {value} failed: {ex}");
            }
        }

        // ---------------------------------------------------------------------------- plumbing

        private static bool IsPerPlayer(ParameterType type) =>
            type == ParameterType.Crystal || type == ParameterType.Wins;

        private static bool GuardSharedTable(ParameterType type, string verb)
        {
            if (!IsPerPlayer(type)) return true;
            MelonLogger.Warning(
                $"[CheatForDev] Refusing to {verb} {type} through the parameter table: the game stores it " +
                "per player, so the write would be invisible. Use the crystal or tournament-win helpers.");
            return false;
        }

        private static NetworkConnection LocalConnection
        {
            get
            {
                try
                {
                    var controller = GameAccess.Parameters;
                    return controller == null ? null : controller.LocalConnection;
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"[CheatForDev] Reading the local connection failed: {ex.Message}");
                    return null;
                }
            }
        }

        // _wins and _crystals are keyed by the player's own id, which the controller maps from the
        // connection's client id in _playerId. On a host that mapping exists as soon as the world is up.
        private static bool TryGetLocalPlayerId(out ulong playerId)
        {
            playerId = 0UL;
            try
            {
                var controller = GameAccess.Parameters;
                var connection = LocalConnection;
                if (controller == null || connection == null) return false;

                var map = controller._playerId;
                if (map == null) return false;

                return map.TryGetValue(connection.ClientId, out playerId);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[CheatForDev] Resolving the local player id failed: {ex.Message}");
                return false;
            }
        }

        // Every write reports what it asked for and what the game actually stored afterwards. A silent
        // disagreement between those two is exactly how the crystal and tournament-win bug survived a
        // whole release, so a mismatch is a warning and is never hidden behind the verbose setting.
        private static void ReportWrite(string label, string route, float asked, float before, float after)
        {
            DevLog.Log($"[CheatForDev] {label}: {before} -> {after} (asked for {asked}, via {route}).");

            if (Mathf.Approximately(before, after) && !Mathf.Approximately(before, asked))
                MelonLogger.Warning(
                    $"[CheatForDev] {label} did not move: still {after} after asking for {asked} via {route}. " +
                    "The game refused the write or stores this value somewhere else.");
        }

        private static bool Guard()
        {
            if (GameAccess.IsServer) return true;
            MelonLogger.Warning("[CheatForDev] Only the host can change parameters.");
            return false;
        }
    }
}

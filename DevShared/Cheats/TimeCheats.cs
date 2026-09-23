using System;
using Il2CppProject.Code.Gameplay.Controllers;
using MelonLoader;
using UnityEngine;

namespace AnimeShopMods.Dev.Cheats
{
    // The shop clock. Opening hours come from the game's own TimeConfig rather than a hard-coded
    // 9-to-21, so this keeps working if the developers move the hours.
    internal static class TimeCheats
    {
        public const int FallbackStartHour = 9;
        public const int FallbackEndHour = 21;

        public static int StartHour
        {
            get
            {
                var config = GameAccess.TimeConfig;
                return config == null ? FallbackStartHour : config.StartHour;
            }
        }

        public static int EndHour
        {
            get
            {
                var config = GameAccess.TimeConfig;
                return config == null ? FallbackEndHour : config.EndHour;
            }
        }

        public static bool IsRunning
        {
            get
            {
                try
                {
                    var controller = GameAccess.Time;
                    return controller != null && controller.IsTimeUpdating;
                }
                catch
                {
                    return false;
                }
            }
        }

        public static float Multiplier
        {
            get
            {
                try
                {
                    var controller = GameAccess.Time;
                    return controller == null ? 1f : controller.DebugTimeMultiplier;
                }
                catch
                {
                    return 1f;
                }
            }
        }

        // Hour and minute of the in-game clock, or -1 when there is no world yet.
        public static bool TryGetClock(out int hour, out int minute)
        {
            hour = -1;
            minute = -1;
            try
            {
                var controller = GameAccess.Time;
                if (controller == null) return false;
                var now = controller.GetCurrentTime();
                hour = now.Hour;
                minute = now.Minute;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static int ServerMinutes
        {
            get
            {
                try
                {
                    var controller = GameAccess.Time;
                    return controller == null ? 0 : controller._serverMinutes.Value;
                }
                catch
                {
                    return 0;
                }
            }
        }

        // SetServerMinutes counts from the moment the shop opens, not from midnight: the probe read
        // _serverMinutes = 0 while the clock showed 09:00 with StartHour = 9. Deriving the offset from the
        // live clock rather than hard-coding StartHour * 60 keeps this correct if the game ever changes it.
        public static int MinutesOffset
        {
            get
            {
                if (!TryGetClock(out var hour, out var minute)) return 0;
                return hour * 60 + minute - ServerMinutes;
            }
        }

        public static void SetRunning(bool running)
        {
            if (!Guard()) return;
            try
            {
                GameAccess.Time.SetUpdateTime(running);
                DevLog.Log($"[CheatForDev] Clock {(running ? "resumed" : "paused")}.");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[CheatForDev] Pausing the clock failed: {ex}");
            }
        }

        public static void SetClock(int hour, int minute)
        {
            if (!Guard()) return;

            int start = StartHour;
            int end = EndHour;
            int wanted = Math.Max(start * 60, Math.Min(end * 60, hour * 60 + minute));

            try
            {
                GameAccess.Time.SetServerMinutes(wanted - MinutesOffset);
                DevLog.Log($"[CheatForDev] Clock set to {wanted / 60:00}:{wanted % 60:00}.");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[CheatForDev] Setting the clock failed: {ex}");
            }
        }

        public static void SetMultiplier(float multiplier)
        {
            if (!Guard()) return;
            try
            {
                GameAccess.Time.DebugSetTimeMultiplier(multiplier);
                DevLog.Log($"[CheatForDev] Time multiplier set to {multiplier}.");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[CheatForDev] Setting the time multiplier failed: {ex}");
            }
        }

        public static void ForceEndDay()
        {
            if (!Guard()) return;
            try
            {
                GameAccess.Time.DebugForceEndDay();
                DevLog.Log("[CheatForDev] Day ended.");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[CheatForDev] Ending the day failed: {ex}");
            }
        }

        // SetNextDay() only winds the clock round to the next morning. The day counter is a separate
        // value that the game raises as part of its end-of-day sequence, which this button skips, so
        // pressing it used to leave the counter where it was - ten presses, still day zero. That matters
        // well beyond the number on screen: the quest system reads the counter, so anything scheduled
        // for a later day, the clothes shop included, could never come due.
        public static void NextDay()
        {
            if (!Guard()) return;
            try
            {
                float before = ProgressCheats.Get(ParameterType.Day);
                GameAccess.Time.SetNextDay();
                ProgressCheats.Add(ParameterType.Day, 1f);
                float after = ProgressCheats.Get(ParameterType.Day);

                DevLog.Log($"[CheatForDev] Skipped to the next day (day counter {before} -> {after}).");

                if (Mathf.Approximately(before, after))
                    MelonLogger.Warning("[CheatForDev] The clock moved on but the day counter did not. " +
                                        "Anything the game schedules by day will stay where it was.");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[CheatForDev] Skipping to the next day failed: {ex}");
            }
        }

        private static bool Guard()
        {
            if (GameAccess.Time == null) return false;
            if (GameAccess.IsServer) return true;
            MelonLogger.Warning("[CheatForDev] Only the host can change the clock.");
            return false;
        }
    }
}

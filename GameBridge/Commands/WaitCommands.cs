using System;
using AnimeShopMods.Dev;
using AnimeShopMods.Dev.Cheats;
using GameBridge.Net;
using UnityEngine;

namespace GameBridge.Commands
{
    // Lets a test say "now let the employees work for a bit" without the caller sleeping blind. Waits
    // on real seconds, or on the game clock, which moves at the game's own pace.
    internal static class WaitCommands
    {
        public const int MaxSeconds = 600;

        public static void Register(Dispatcher d) => d.Register("wait", (Request request) => Wait(d, request));

        private static void Wait(Dispatcher d, Request request)
        {
            var args = request.Args;
            float seconds = Math.Min(args.Value<float?>("seconds") ?? 0f, MaxSeconds);
            int gameMinutes = args.Value<int?>("gameMinutes") ?? 0;
            if (seconds <= 0f && gameMinutes <= 0)
                throw new ArgumentException("Give seconds or gameMinutes.");
            if (gameMinutes > 0 && !GameAccess.InGame)
                throw new InvalidOperationException("No store is loaded, so there is no game clock to wait on.");

            float started = Time.realtimeSinceStartup;
            float deadline = started + (seconds > 0f ? seconds : MaxSeconds);
            int startMinutes = gameMinutes > 0 ? TimeCheats.ServerMinutes : 0;

            d.Defer(request, () =>
            {
                float now = Time.realtimeSinceStartup;
                bool clockDone = gameMinutes > 0 && TimeCheats.ServerMinutes - startMinutes >= gameMinutes;
                if (!clockDone && now < deadline) return false;

                request.Reply(new
                {
                    waitedSeconds = Math.Round(now - started, 2),
                    gameMinutesPassed = gameMinutes > 0 ? TimeCheats.ServerMinutes - startMinutes : 0,
                    reason = clockDone ? "game clock" : "seconds",
                });
                return true;
            });
        }
    }
}

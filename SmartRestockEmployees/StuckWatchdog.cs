using System.Collections.Generic;
using Il2CppProject.Code.Gameplay.AI.Employee;
using MelonLoader;
using UnityEngine;

namespace SmartRestockEmployees
{
    // Recovers employees that stand still while they should be walking to a shelf or disposing a box,
    // for example when they get caught on a trash can. It only uses the game's own recovery methods:
    // first HandleMoveFailed (the normal failed-movement path), then releasing the task if that did not help.
    internal static class StuckWatchdog
    {
        private const float TickInterval = 1f;
        private const float MoveTolerance = 0.5f;

        private sealed class Track
        {
            public Vector3 Anchor;
            public float StillSince;
            public int Attempts;
        }

        private static readonly Dictionary<System.IntPtr, Track> Tracks = new Dictionary<System.IntPtr, Track>();
        private static float _nextTick;

        public static void Reset()
        {
            Tracks.Clear();
            _nextTick = 0f;
        }

        public static void Tick()
        {
            float now = Time.time;
            if (now < _nextTick) return;
            _nextTick = now + TickInterval;

            try
            {
                var seen = new HashSet<System.IntPtr>();

                foreach (var sorting in Object.FindObjectsOfType<EmployeeSortingController>())
                {
                    if (sorting == null || !sorting.IsServerInitialized) continue;
                    seen.Add(sorting.Pointer);
                    bool busy = sorting._isMovingToShelf || sorting._isDisposingEmptyPickup;
                    Check(sorting.Pointer, sorting.transform.position, busy, now, "sorting",
                        () => sorting.HandleMoveFailed(),
                        () => sorting.ReleaseCurrentPickupAndSetIdle());
                }

                foreach (var storage in Object.FindObjectsOfType<EmployeeStorageController>())
                {
                    if (storage == null || !storage.IsServerInitialized) continue;
                    seen.Add(storage.Pointer);
                    bool busy = storage._isDisposingEmptyPickup;
                    Check(storage.Pointer, storage.transform.position, busy, now, "storage",
                        () => storage.HandleMoveFailed(),
                        () => storage.ReleaseCurrentPickupAndSetIdle());
                }

                if (Tracks.Count > seen.Count)
                {
                    var stale = new List<System.IntPtr>();
                    foreach (var key in Tracks.Keys)
                        if (!seen.Contains(key)) stale.Add(key);
                    foreach (var key in stale)
                        Tracks.Remove(key);
                }
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"[StuckWatchdog] Tick failed: {ex}");
            }
        }

        private static void Check(System.IntPtr key, Vector3 position, bool busy, float now, string role,
            System.Action retryMove, System.Action releaseTask)
        {
            if (!busy)
            {
                Tracks.Remove(key);
                return;
            }

            if (!Tracks.TryGetValue(key, out var track))
            {
                Tracks[key] = new Track { Anchor = position, StillSince = now };
                return;
            }

            if ((position - track.Anchor).sqrMagnitude > MoveTolerance * MoveTolerance)
            {
                track.Anchor = position;
                track.StillSince = now;
                track.Attempts = 0;
                return;
            }

            if (now - track.StillSince < Main.StuckSeconds) return;

            track.StillSince = now;
            if (track.Attempts == 0)
            {
                MelonLogger.Msg($"[StuckWatchdog] {role} employee stuck at {position}, retrying movement.");
                retryMove();
            }
            else
            {
                MelonLogger.Msg($"[StuckWatchdog] {role} employee still stuck at {position}, releasing current task.");
                releaseTask();
            }
            track.Attempts++;
        }
    }
}

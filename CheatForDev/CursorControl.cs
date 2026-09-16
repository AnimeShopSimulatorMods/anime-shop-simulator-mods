using System;
using Il2CppInterop.Runtime;
using Il2CppProject.Code.Core.UI;
using MelonLoader;
using UnityEngine;

namespace CheatForDev
{
    // The game hands out cursor "leases" so several systems can ask for a visible mouse without fighting
    // each other. Borrowing one is far safer than writing Cursor.lockState behind the game's back; the
    // manual route is only used when the service cannot be reached.
    internal static class CursorControl
    {
        private static ICursorLease _lease;
        private static Il2CppSystem.Object _owner;
        private static bool _manualFallback;

        public static void Acquire()
        {
            if (_lease != null || _manualFallback) return;

            try
            {
                var ui = GameAccess.Ui;
                if (ui != null)
                {
                    _owner ??= new Il2CppSystem.Object();
                    _lease = ui.AcquireCursor(_owner);
                    if (_lease != null) return;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[CheatForDev] Could not borrow the game's cursor ({ex.GetType().Name}); showing it directly.");
            }

            _manualFallback = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public static void Release()
        {
            if (_lease != null)
            {
                try
                {
                    _lease.Cast<Il2CppSystem.IDisposable>().Dispose();
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"[CheatForDev] Returning the cursor failed: {ex.Message}");
                }
                _lease = null;
                return;
            }

            if (!_manualFallback) return;
            _manualFallback = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        // Keeps the cursor visible while the menu is open even if the game re-hides it every frame.
        public static void Tick()
        {
            if (_manualFallback && !Cursor.visible)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        public static void Reset()
        {
            _lease = null;
            _manualFallback = false;
        }
    }
}

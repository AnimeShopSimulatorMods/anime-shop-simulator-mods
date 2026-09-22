using System;
using System.Linq;
using GameBridge.Input;
using GameBridge.Net;
using UnityEngine;

namespace GameBridge.Commands
{
    internal static class InputCommands
    {
        private const float MaxHoldSeconds = 30f;

        public static void Register(Dispatcher d)
        {
            d.Register("key_press", (Request request) => Press(d, request));
            d.Register("key_down", args => Focused(() => Win32Input.KeyDown((string)args["key"])));
            d.Register("key_up", args => Focused(() => Win32Input.KeyUp((string)args["key"])));
            d.Register("key_release_all", _ =>
            {
                Win32Input.ReleaseAll();
                return new { held = Win32Input.HeldKeys.ToArray() };
            });
            d.Register("type_text", args => Focused(() => Win32Input.TypeText((string)args["text"])));
            d.Register("mouse_move", args => Focused(() => Win32Input.MouseMoveBy((int)args["dx"], (int)args["dy"])));
            d.Register("mouse_click", args => Focused(() => Win32Input.Click((string)args["button"] == "right")));
        }

        // Down now, up after the hold time, answered once the key is back up.
        private static void Press(Dispatcher d, Request request)
        {
            var key = (string)request.Args["key"];
            float hold = Mathf.Clamp(request.Args.Value<float?>("holdSeconds") ?? 0.1f, 0.02f, MaxHoldSeconds);

            var focusProblem = Win32Input.EnsureFocus();
            if (focusProblem != null)
            {
                request.Fail(focusProblem);
                return;
            }

            Win32Input.KeyDown(key);
            float release = Time.realtimeSinceStartup + hold;
            d.Defer(request, () =>
            {
                if (Time.realtimeSinceStartup < release) return false;
                Win32Input.KeyUp(key);
                request.Reply(new { key, heldSeconds = hold });
                return true;
            });
        }

        private static object Focused(Action send)
        {
            var focusProblem = Win32Input.EnsureFocus();
            if (focusProblem != null) throw new InvalidOperationException(focusProblem);
            send();
            return new { sent = true, held = Win32Input.HeldKeys.ToArray() };
        }
    }
}

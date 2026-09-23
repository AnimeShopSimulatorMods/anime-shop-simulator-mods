using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace GameBridge.Input
{
    // Hardware-looking input through SendInput. The game ships Rewired, InputSystem and legacy Input,
    // and scan codes reach all three, so this does not need to know which one reads the controls.
    // SendInput goes to whatever window is in front, which is why every send checks focus first.
    internal static class Win32Input
    {
        private const int InputMouse = 0;
        private const int InputKeyboard = 1;
        // "Flag" suffix because a plain KeyUp would collide with the KeyUp(string) method below (CS0102:
        // a const field and a method cannot share a name in the same type).
        private const uint KeyUpFlag = 0x0002;
        private const uint ScanCode = 0x0008;
        private const uint Unicode = 0x0004;
        private const uint ExtendedKey = 0x0001;
        private const uint MouseMove = 0x0001;
        private const uint LeftDown = 0x0002, LeftUp = 0x0004, RightDown = 0x0008, RightUp = 0x0010;
        private const byte VkMenu = 0x12;

        private static readonly HashSet<string> Held = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public static IEnumerable<string> HeldKeys => Held;

        public static void KeyDown(string key)
        {
            Send(Key(key, up: false));
            Held.Add(key);
        }

        public static void KeyUp(string key)
        {
            Send(Key(key, up: true));
            Held.Remove(key);
        }

        // Never leave a key stuck down when the bridge stops or a test aborts.
        public static void ReleaseAll()
        {
            foreach (var key in new List<string>(Held))
            {
                try
                {
                    KeyUp(key);
                }
                catch
                {
                    // Best effort on the way out.
                }
            }
            Held.Clear();
        }

        public static void TypeText(string text)
        {
            foreach (char c in text)
            {
                Send(new NativeInput { type = InputKeyboard, u = new InputUnion { ki = new KeyboardInput { wScan = c, dwFlags = Unicode } } });
                Send(new NativeInput { type = InputKeyboard, u = new InputUnion { ki = new KeyboardInput { wScan = c, dwFlags = Unicode | KeyUpFlag } } });
            }
        }

        public static void MouseMoveBy(int dx, int dy) =>
            Send(new NativeInput { type = InputMouse, u = new InputUnion { mi = new MouseInput { dx = dx, dy = dy, dwFlags = MouseMove } } });

        public static void Click(bool right)
        {
            Send(new NativeInput { type = InputMouse, u = new InputUnion { mi = new MouseInput { dwFlags = right ? RightDown : LeftDown } } });
            Send(new NativeInput { type = InputMouse, u = new InputUnion { mi = new MouseInput { dwFlags = right ? RightUp : LeftUp } } });
        }

        // Returns null when the game is in front, or the reason it could not be brought there.
        public static string EnsureFocus()
        {
            var window = Process.GetCurrentProcess().MainWindowHandle;
            if (window == IntPtr.Zero) return "The game has no main window handle.";
            if (GetForegroundWindow() == window) return null;

            // Windows only lets the process that last received input change the foreground window. A
            // synthetic Alt tap makes this process that one, which is the documented way around it.
            keybd_event(VkMenu, 0, 0, UIntPtr.Zero);
            keybd_event(VkMenu, 0, KeyUpFlag, UIntPtr.Zero);
            ShowWindow(window, 9 /* SW_RESTORE */);
            SetForegroundWindow(window);

            return GetForegroundWindow() == window
                ? null
                : "Windows would not bring the game window to the front, so input was not sent (it would " +
                  "have gone to another window). Click the game window once, then retry.";
        }

        private static NativeInput Key(string name, bool up)
        {
            var (vk, extended) = VirtualKey(name);
            ushort scan = (ushort)MapVirtualKey(vk, 0 /* MAPVK_VK_TO_VSC */);
            uint flags = ScanCode | (up ? KeyUpFlag : 0) | (extended ? ExtendedKey : 0);
            return new NativeInput { type = InputKeyboard, u = new InputUnion { ki = new KeyboardInput { wScan = scan, dwFlags = flags } } };
        }

        private static (uint vk, bool extended) VirtualKey(string name)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("No key given.");
            var n = name.Trim();

            if (n.Length == 1)
            {
                char c = char.ToUpperInvariant(n[0]);
                if (c >= 'A' && c <= 'Z' || c >= '0' && c <= '9') return (c, false);
            }
            if ((n[0] == 'F' || n[0] == 'f') && int.TryParse(n.Substring(1), out int f) && f >= 1 && f <= 12)
                return ((uint)(0x70 + f - 1), false);

            switch (n.ToLowerInvariant())
            {
                case "space": return (0x20, false);
                case "enter": case "return": return (0x0D, false);
                case "escape": case "esc": return (0x1B, false);
                case "tab": return (0x09, false);
                case "backspace": return (0x08, false);
                case "shift": case "leftshift": return (0xA0, false);
                case "ctrl": case "control": case "leftctrl": return (0xA2, false);
                case "alt": case "leftalt": return (0xA4, false);
                case "up": return (0x26, true);
                case "down": return (0x28, true);
                case "left": return (0x25, true);
                case "right": return (0x27, true);
                case "insert": return (0x2D, true);
                case "delete": return (0x2E, true);
                case "home": return (0x24, true);
                case "end": return (0x23, true);
                case "pageup": return (0x21, true);
                case "pagedown": return (0x22, true);
            }
            throw new ArgumentException($"Unknown key '{name}'. Use a letter, digit, F1-F12, or a name like Space, Shift, Escape, Up.");
        }

        private static void Send(NativeInput input)
        {
            var inputs = new[] { input };
            if (SendInput(1, inputs, Marshal.SizeOf(typeof(NativeInput))) != 1)
                throw new InvalidOperationException($"SendInput failed (Win32 error {Marshal.GetLastWin32Error()}).");
        }

        // Named NativeInput, not Input, so it never reads as a reference to this file's own namespace
        // (GameBridge.Input) when skimmed next to it.
        [StructLayout(LayoutKind.Sequential)]
        private struct NativeInput
        {
            public int type;
            public InputUnion u;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)] public MouseInput mi;
            [FieldOffset(0)] public KeyboardInput ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MouseInput
        {
            public int dx, dy;
            public uint mouseData, dwFlags, time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KeyboardInput
        {
            public ushort wVk, wScan;
            public uint dwFlags, time;
            public IntPtr dwExtraInfo;
        }

        [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, NativeInput[] inputs, int size);
        [DllImport("user32.dll")] private static extern uint MapVirtualKey(uint code, uint mapType);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr window, int command);
        [DllImport("user32.dll")] private static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    }
}

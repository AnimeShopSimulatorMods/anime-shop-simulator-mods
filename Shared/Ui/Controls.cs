using System;
using UnityEngine;

namespace AnimeShopMods.Ui
{
    // Every control the menu uses, each one able to fall back to something this game's stripped IMGUI
    // can still draw. Typing is the first casualty: GUILayout.TextField is usually gone, so numbers are
    // entered with stepper buttons instead. That turns out to be faster for a cheat menu anyway.
    internal static class Controls
    {
        public static void Label(string text)
        {
            if (!GuiCaps.Label) return;
            GUILayout.Label(text);
        }

        public static void Header(string text)
        {
            Label($"--- {text} ---");
        }

        public static void Space(float pixels = 6f)
        {
            if (GuiCaps.Space) GUILayout.Space(pixels);
        }

        public static bool Button(string text, float width = 0f)
        {
            if (!GuiCaps.Button) return false;
            return width > 0f && GuiCaps.Width
                ? GUILayout.Button(text, GUILayout.Width(width))
                : GUILayout.Button(text);
        }

        public static void BeginRow()
        {
            if (GuiCaps.Horizontal) GUILayout.BeginHorizontal();
        }

        public static void EndRow()
        {
            if (GuiCaps.Horizontal) GUILayout.EndHorizontal();
        }

        // Falls back to a pair of buttons, which is always available when Toggle is not.
        public static bool Toggle(bool value, string text)
        {
            if (GuiCaps.Toggle) return GUILayout.Toggle(value, "  " + text);

            BeginRow();
            Label($"{text}: {(value ? "on" : "off")}");
            if (Button(value ? "Turn off" : "Turn on", 110f)) value = !value;
            EndRow();
            return value;
        }

        // A number the player changes with buttons. steps are offered as -step / +step pairs.
        public static float Stepper(string label, float value, float[] steps, string format = "0")
        {
            BeginRow();
            Label($"{label}: {value.ToString(format)}");
            EndRow();

            BeginRow();
            for (int i = steps.Length - 1; i >= 0; i--)
                if (Button($"-{Short(steps[i])}", 62f)) value -= steps[i];
            for (int i = 0; i < steps.Length; i++)
                if (Button($"+{Short(steps[i])}", 62f)) value += steps[i];
            EndRow();
            return value;
        }

        // A row of fixed values to jump straight to.
        public static bool Presets(string label, float[] values, out float chosen, string format = "0")
        {
            chosen = 0f;
            bool picked = false;

            BeginRow();
            Label(label);
            for (int i = 0; i < values.Length; i++)
            {
                if (!Button(values[i].ToString(format), 80f)) continue;
                chosen = values[i];
                picked = true;
            }
            EndRow();
            return picked;
        }

        public static int Tabs(int current, string[] names)
        {
            if (GuiCaps.Toolbar) return GUILayout.Toolbar(current, names);

            BeginRow();
            for (int i = 0; i < names.Length; i++)
            {
                string text = i == current ? $"[{names[i]}]" : names[i];
                if (Button(text, 84f)) current = i;
            }
            EndRow();
            return current;
        }

        public static Vector2 BeginScroll(Vector2 scroll)
        {
            return GuiCaps.ScrollView ? GUILayout.BeginScrollView(scroll) : scroll;
        }

        public static void EndScroll()
        {
            if (GuiCaps.ScrollView) GUILayout.EndScrollView();
        }

        private static string Short(float value)
        {
            if (Mathf.Abs(value) >= 1_000_000f) return $"{value / 1_000_000f:0.##}M";
            if (Mathf.Abs(value) >= 1_000f) return $"{value / 1_000f:0.##}k";
            return value.ToString("0.##");
        }
    }
}

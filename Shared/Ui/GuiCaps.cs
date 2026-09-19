using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;

namespace AnimeShopMods.Ui
{
    // The game ships with most of IMGUI stripped, and Il2CppInterop can only rebuild part of it: calling
    // a missing control throws NotSupportedException("Method unstripping failed") every single frame.
    // So the menu asks first. Each control is called once inside a real OnGUI pass, off-screen, and the
    // menu is then built only from what answered. Re-run this after a game update; the log line says
    // exactly what changed.
    internal static class GuiCaps
    {
        private static readonly Rect Scratch = new Rect(-4000f, -4000f, 300f, 400f);

        public static bool Surveyed { get; private set; }

        private static bool _logged;

        public static bool Box { get; private set; }
        public static bool SkinBox { get; private set; }
        public static bool Label { get; private set; }
        public static bool Button { get; private set; }
        public static bool TextField { get; private set; }
        public static bool Toggle { get; private set; }
        public static bool Toolbar { get; private set; }
        public static bool Slider { get; private set; }
        public static bool Space { get; private set; }
        public static bool Width { get; private set; }
        public static bool Horizontal { get; private set; }
        public static bool Vertical { get; private set; }
        public static bool ScrollView { get; private set; }
        public static bool Events { get; private set; }

        // Needed by any menu that wants to look like part of the game rather than a debug overlay.
        public static bool CustomStyle { get; private set; }
        public static bool DrawTexture { get; private set; }
        public static Font GameFont { get; private set; }

        public static void Reset()
        {
            Surveyed = false;
            _logged = false;
            GameFont = null;
        }

        // Called once per event pass of a single frame. The checks are repeated on each pass on purpose:
        // IMGUI matches controls between the layout and repaint passes and throws if they differ.
        public static void Survey()
        {
            Surveyed = true;

            var report = new List<string>();

            Box = Check(report, "GUI.Box", () => GUI.Box(Scratch, "x"));
            Events = Check(report, "Event.current", () => { var _ = Event.current.type; });
            SkinBox = Check(report, "GUI.skin.box", () => { var _ = GUI.skin.box; });

            Label = CheckLayout(report, "GUILayout.Label", () => GUILayout.Label("x"));
            Button = CheckLayout(report, "GUILayout.Button", () => GUILayout.Button("x"));
            TextField = CheckLayout(report, "GUILayout.TextField", () => GUILayout.TextField("x"));
            Toggle = CheckLayout(report, "GUILayout.Toggle", () => GUILayout.Toggle(false, "x"));
            Toolbar = CheckLayout(report, "GUILayout.Toolbar", () => GUILayout.Toolbar(0, new[] { "a", "b" }));
            Slider = CheckLayout(report, "GUILayout.HorizontalSlider", () => GUILayout.HorizontalSlider(0.5f, 0f, 1f));
            Space = CheckLayout(report, "GUILayout.Space", () => GUILayout.Space(4f));
            Width = CheckLayout(report, "GUILayout.Width", () => GUILayout.Label("x", GUILayout.Width(50f)));
            Horizontal = CheckLayout(report, "GUILayout.BeginHorizontal", () =>
            {
                GUILayout.BeginHorizontal();
                GUILayout.EndHorizontal();
            });
            Vertical = CheckLayout(report, "GUILayout.BeginVertical", () =>
            {
                GUILayout.BeginVertical();
                GUILayout.EndVertical();
            });
            ScrollView = CheckLayout(report, "GUILayout.BeginScrollView", () =>
            {
                GUILayout.BeginScrollView(Vector2.zero);
                GUILayout.EndScrollView();
            });

            CustomStyle = Check(report, "new GUIStyle", () =>
            {
                var style = new GUIStyle(GUI.skin.button) { fontSize = 13 };
                style.normal.background = Texture2D.whiteTexture;
            });

            DrawTexture = Check(report, "GUI.DrawTexture", () =>
                GUI.DrawTexture(Scratch, Texture2D.whiteTexture));

            GameFont = FindFont(report);

            if (_logged) return;
            _logged = true;

            int missing = 0;
            foreach (var line in report)
                if (line.StartsWith(Missing)) missing++;

            // Worded carefully: a stripped control is the expected result here, not a failure, and every
            // menu is built to work without it. An earlier version printed "FAIL" next to a
            // NotSupportedException and players reasonably read that as the mod being broken.
            MelonLogger.Msg("===== [ModUi] IMGUI survey =====");
            MelonLogger.Msg($"  {report.Count - missing} of {report.Count} controls are usable in this build. " +
                            "Anything missing is normal - the menu leaves it out and uses a substitute.");
            foreach (var line in report) MelonLogger.Msg("  " + line);
            MelonLogger.Msg("===== [ModUi] IMGUI survey end =====");
        }

        private const string Present = "usable      ";
        private const string Missing = "not in build";

        private static bool Check(List<string> report, string label, Action call)
        {
            try
            {
                call();
                report.Add($"{Present}  {label}");
                return true;
            }
            catch (Exception ex)
            {
                report.Add($"{Missing}  {label} ({ex.GetType().Name})");
                return false;
            }
        }

        // IMGUI's built-in font is small and plain. The game ships nicer ones and borrowing one costs
        // nothing, but only a dynamic font is safe: a static bitmap font renders any character outside
        // the atlas it was baked with as a blank box.
        private static Font FindFont(List<string> report)
        {
            try
            {
                var fonts = Resources.FindObjectsOfTypeAll<Font>();
                if (fonts == null || fonts.Length == 0)
                {
                    report.Add($"{Missing}  game font (none loaded)");
                    return null;
                }

                foreach (var font in fonts)
                {
                    if (font == null || !font.dynamic) continue;
                    report.Add($"{Present}  game font: {font.name}");
                    return font;
                }

                report.Add($"{Missing}  game font (none dynamic; the built-in one is used instead)");
                return null;
            }
            catch (Exception ex)
            {
                report.Add($"{Missing}  game font ({ex.GetType().Name})");
                return null;
            }
        }

        private static bool CheckLayout(List<string> report, string label, Action call)
        {
            bool areaOpen = false;
            try
            {
                GUILayout.BeginArea(Scratch);
                areaOpen = true;
                call();
                report.Add($"{Present}  {label}");
                return true;
            }
            catch (Exception ex)
            {
                report.Add($"{Missing}  {label} ({ex.GetType().Name})");
                return false;
            }
            finally
            {
                if (areaOpen)
                {
                    try { GUILayout.EndArea(); }
                    catch { /* the failure already unwound the area */ }
                }
            }
        }
    }
}

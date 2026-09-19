using UnityEngine;

namespace AnimeShopMods.Ui
{
    // IMGUI ships one look: small grey Arial on a grey box. Everything here exists to get past that,
    // because a panel players open mid-game should not look like a debug overlay.
    //
    // Textures are generated in code rather than shipped as files so each mod stays a single DLL that
    // players drop into Mods\ -- a second file to install is a reliable source of broken installs.
    //
    // Nothing here is safe to touch outside OnGUI: GUI.skin and GUIStyle construction both need the
    // IMGUI context. EnsureBuilt is the only entry point and it is idempotent.
    internal static class Skin
    {
        public static readonly Color Panel = new Color32(0x1B, 0x1A, 0x22, 0xF5);
        public static readonly Color Raised = new Color32(0x24, 0x22, 0x2D, 0xFF);
        public static readonly Color Line = new Color32(0x3A, 0x37, 0x45, 0xFF);
        public static readonly Color Accent = new Color32(0xF5, 0x8C, 0xC4, 0xFF);
        public static readonly Color OnAccent = new Color32(0x3A, 0x10, 0x29, 0xFF);
        public static readonly Color Text = new Color32(0xF3, 0xF1, 0xF7, 0xFF);
        public static readonly Color TextBody = new Color32(0xE2, 0xDE, 0xEC, 0xFF);
        public static readonly Color TextDim = new Color32(0x8C, 0x87, 0x99, 0xFF);
        public static readonly Color Outline = new Color32(0x56, 0x52, 0x6B, 0xFF);
        public static readonly Color Warn = new Color32(0xEF, 0x9F, 0x27, 0xFF);

        public static bool Ready { get; private set; }

        public static GUIStyle PanelBox { get; private set; }
        public static GUIStyle RaisedBox { get; private set; }
        public static GUIStyle Title { get; private set; }
        public static GUIStyle Body { get; private set; }
        public static GUIStyle Hint { get; private set; }
        public static GUIStyle Primary { get; private set; }
        public static GUIStyle Secondary { get; private set; }
        public static Texture2D Pixel { get; private set; }

        public static void Reset()
        {
            Ready = false;
        }

        public static void EnsureBuilt()
        {
            if (Ready || !GuiCaps.CustomStyle) return;

            Pixel = Solid(Color.white);

            var panelTex = Rounded(Panel, Line, 6);
            var raisedTex = Rounded(Raised, Raised, 4);
            var accentTex = Rounded(Accent, Accent, 4);
            var accentHoverTex = Rounded(Lift(Accent, 0.10f), Lift(Accent, 0.10f), 4);
            var outlineTex = Rounded(new Color(0f, 0f, 0f, 0f), Outline, 4);
            var outlineHoverTex = Rounded(Raised, Outline, 4);

            PanelBox = BoxStyle(panelTex, 8, 14, 12);
            RaisedBox = BoxStyle(raisedTex, 6, 10, 8);

            Title = LabelStyle(15, Text);
            Body = LabelStyle(13, TextBody);
            Hint = LabelStyle(12, TextDim);
            Hint.wordWrap = true;

            Primary = ButtonStyle(accentTex, accentHoverTex, OnAccent, OnAccent);
            Secondary = ButtonStyle(outlineTex, outlineHoverTex, TextBody, Text);

            Ready = true;
        }

        private static GUIStyle BoxStyle(Texture2D texture, int border, int padX, int padY)
        {
            var style = new GUIStyle(GUI.skin.box)
            {
                border = new RectOffset(border, border, border, border),
                padding = new RectOffset(padX, padX, padY, padY),
                margin = new RectOffset(0, 0, 0, 0),
            };
            style.normal.background = texture;
            return style;
        }

        // Named LabelStyle rather than Label: a method cannot share a name with the Text colour field,
        // and the suffix keeps it obvious which GUI.skin entry it derives from.
        private static GUIStyle LabelStyle(int size, Color color)
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = size,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(0, 0, 2, 2),
            };
            if (GuiCaps.GameFont != null) style.font = GuiCaps.GameFont;
            style.normal.textColor = color;
            return style;
        }

        private static GUIStyle ButtonStyle(Texture2D normal, Texture2D hover, Color text, Color hoverText)
        {
            var style = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                border = new RectOffset(6, 6, 6, 6),
                padding = new RectOffset(12, 12, 9, 9),
                margin = new RectOffset(0, 0, 0, 0),
                alignment = TextAnchor.MiddleCenter,
            };
            if (GuiCaps.GameFont != null) style.font = GuiCaps.GameFont;
            style.normal.background = normal;
            style.hover.background = hover;
            style.active.background = hover;
            style.normal.textColor = text;
            style.hover.textColor = hoverText;
            style.active.textColor = hoverText;
            return style;
        }

        private static Texture2D Solid(Color color)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            Keep(texture);
            return texture;
        }

        // A 9-slice tile: the corner bands stay put while the edges and the single-pixel centre stretch.
        // Size is 2*radius+2 so that centre row and column sit between the two corner bands.
        private static Texture2D Rounded(Color fill, Color border, int radius)
        {
            int size = radius * 2 + 2;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var clear = new Color(0f, 0f, 0f, 0f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x < radius ? radius - x : (x > size - radius - 1 ? x - (size - radius - 1) : 0f);
                    float dy = y < radius ? radius - y : (y > size - radius - 1 ? y - (size - radius - 1) : 0f);
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);

                    if (distance > radius) texture.SetPixel(x, y, clear);
                    else if (distance > radius - 1.2f) texture.SetPixel(x, y, border);
                    else texture.SetPixel(x, y, fill);
                }
            }

            texture.Apply();
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            Keep(texture);
            return texture;
        }

        // Without this Unity destroys the texture on the next scene load and the panel draws nothing
        // but holes after the player loads a save.
        private static void Keep(Texture2D texture)
        {
            texture.hideFlags = HideFlags.HideAndDontSave;
        }

        private static Color Lift(Color color, float amount)
        {
            return new Color(
                Mathf.Min(1f, color.r + amount),
                Mathf.Min(1f, color.g + amount),
                Mathf.Min(1f, color.b + amount),
                color.a);
        }
    }
}

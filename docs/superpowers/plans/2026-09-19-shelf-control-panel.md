# Shelf Control Panel Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give Smart Restock Employees players an in-game panel to stop employees refilling a shelf, clear a shelf's remembered product, and send a shelf's stock back to a delivery zone of their choice.

**Architecture:** A store-wide rule in `SlotRules` gates whether never-filled slots may be used, read from two existing hooks in `SortingPatches`. Shelf actions write game state directly (`_lastDefinitionId`, `OrderController.CreateShelfPickup`) so the mod keeps no save file of its own. The panel is IMGUI rendered through a hand-built skin, sharing UI source with CheatForDev via a `Shared/` folder compiled into both assemblies.

**Tech Stack:** C# net48, MelonLoader 0.7.x, HarmonyX, Il2CppInterop, Unity IMGUI (`OnGUI`). Game: Anime Shop Simulator (Il2Cpp).

**Spec:** `docs/superpowers/specs/2026-09-19-shelf-control-panel-design.md`

---

## Read this before Task 1

**There is no unit test framework here and there cannot be one.** Every type this code touches (`ProductPricePlace`, `OrderController`, `ShelfProducts`) is an Il2Cpp type that only exists inside a running game process. It cannot be constructed, mocked, or loaded by a test runner. Do not try to add xUnit or NUnit — you will waste an afternoon and end up testing nothing real.

Verification in this plan is therefore two gates on every task:

1. **Compile gate** — the build must succeed. This is not a formality: Il2CppInterop signatures are easy to get wrong and the compiler catches most of it.
2. **In-game gate** — a named, observable check with expected log output or on-screen result.

Every task below states both. Do not mark a task done on the compile gate alone.

### Build command

```bash
powershell -ExecutionPolicy Bypass -Command "& (& \"\${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe\" -latest -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1) AnimeShopMods.slnx /restore /p:Configuration=Release /v:minimal /nologo"
```

### Deploy command

`$GamePath` comes from `Directory.Build.local.props`, default `E:\SteamLibrary\steamapps\common\Anime Shop Simulator`.

```bash
cp SmartRestockEmployees/bin/Release/SmartRestockEmployees.dll "E:/SteamLibrary/steamapps/common/Anime Shop Simulator/Mods/"
```

### Reading the log

MelonLoader writes to `<GamePath>/MelonLoader/Latest.log`. Turn on the mod's own logging first — set `VerboseLogs = true` in `<GamePath>/UserData/MelonPreferences.cfg` under `[SmartRestockEmployees]`, then launch. Every `Main.Log` line is gated on it.

### File structure

| File | Responsibility |
| --- | --- |
| `Shared/Ui/GuiCaps.cs` | Which IMGUI controls this build actually has. Moved from CheatForDev. |
| `Shared/Ui/Controls.cs` | Degrading wrappers over IMGUI controls. Moved from CheatForDev. |
| `Shared/Ui/Skin.cs` | **New.** Textures and `GUIStyle` objects for the panel. Built once. |
| `Shared/CursorControl.cs` | Borrowing the game's cursor lease. Moved from CheatForDev. |
| `Shared/Game/ShelfFinder.cs` | **New.** Scanning shelves and the crosshair lookup. Extracted from ShelfCheats. |
| `Shared/Game/ShelfMemory.cs` | **New.** Reading and clearing a slot's remembered product. |
| `Shared/Game/DeliveryReturn.cs` | **New.** Turning shelf stock back into boxes at a chosen zone. |
| `SmartRestockEmployees/SlotRules.cs` | Modify. Add the free-slot rule. |
| `SmartRestockEmployees/SortingPatches.cs` | Modify. Two call sites read the new rule. |
| `SmartRestockEmployees/Main.cs` | Modify. New preferences, hotkey, `OnGUI`. |
| `SmartRestockEmployees/Ui/ShelfPanel.cs` | **New.** The panel itself: aim view, list view, confirmation. |

Each mod needs its own `GameAccess`-style lookup. CheatForDev's `GameAccess` stays where it is; `Shared/Game/*` takes the controllers it needs as arguments instead of reaching for a locator, so it does not care which mod hosts it.

---

## Task 1: The free-slot rule

Smallest change, largest share of the two user requests, and it ships value with no UI at all — players can set it in `MelonPreferences.cfg` the moment this task lands.

**Files:**
- Modify: `SmartRestockEmployees/SlotRules.cs`
- Modify: `SmartRestockEmployees/Main.cs:27-42`
- Modify: `SmartRestockEmployees/SortingPatches.cs:368-383`
- Modify: `SmartRestockEmployees/SortingPatches.cs:472-481`

- [x] **Step 1: ~~Add the rule to SlotRules~~ — dropped during execution**

The plan originally added `IsFreeSlot` and `AllowsFill` helpers here. Steps 3 and 4 then read
`Main.FillFreeSlots` directly, because both call sites already hold the slot's product id and
calling a helper would recompute it. That left the helpers dead, so they were not added. The rule
has one definition either way: the preference itself.

- [ ] **Step 2: Add the preference**

In `SmartRestockEmployees/Main.cs`, add the field next to `_keepEmptySlotProduct`:

```csharp
        private static MelonPreferences_Entry<bool> _fillFreeSlots;
```

Add the accessor next to `KeepEmptySlotProduct`:

```csharp
        public static bool FillFreeSlots => _fillFreeSlots == null || _fillFreeSlots.Value;
```

Add the entry in `OnInitializeMelon`, directly after the `_keepEmptySlotProduct` entry:

```csharp
            _fillFreeSlots = category.CreateEntry("FillFreeSlots", true, "Fill free slots",
                "Let employees put stock in slots that have never held anything. " +
                "Turn this off to keep bare shelves bare.");
```

The default is `true` on purpose. It preserves today's behaviour, and a default of `false` would mean a newly bought shelf is never stocked again for anyone who updates without reading the release notes.

- [ ] **Step 3: Gate the slot search**

In `SmartRestockEmployees/SortingPatches.cs`, in `HavePointsPatch.Allows`, replace this line:

```csharp
            if (!SlotRules.HasProduct(slotProductId)) return true;
```

with:

```csharp
            if (!SlotRules.HasProduct(slotProductId)) return Main.FillFreeSlots;
```

- [ ] **Step 4: Gate the shelf ranking**

In the same file, in `ResolveProductForSlot`, replace this line:

```csharp
            var productPlace = place.ProductPlace;
```

with:

```csharp
            if (!Main.FillFreeSlots) return SlotRules.NoProduct;

            var productPlace = place.ProductPlace;
```

Step 3 alone is not enough. Without step 4, a shelf of free slots still ranks as "emptiest" in `ChooseTarget`, and the employee walks all the way there to find nothing it is allowed to do.

- [ ] **Step 5: Build**

Run the build command from the header.
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 6: In-game check — rule on (default)**

Deploy, launch, load a save. Place a brand new shelf with no stock ever on it, put a matching box in storage.
Expected: an employee stocks the new shelf, exactly as before this change.

- [ ] **Step 7: In-game check — rule off**

Quit. Set `FillFreeSlots = false` in `<GamePath>/UserData/MelonPreferences.cfg`. Relaunch.
Expected: the employee never walks to the new shelf. With `VerboseLogs = true`, `Latest.log` shows `[SmartSorting] No slot worth restocking` or a target on a different shelf — never a target on the untouched one.

- [ ] **Step 8: Commit**

```bash
git add SmartRestockEmployees/SlotRules.cs SmartRestockEmployees/Main.cs SmartRestockEmployees/SortingPatches.cs
git commit -m "Add FillFreeSlots rule for never-filled shelf slots

Slots that have never held stock had no remembered product, so they were
treated as free and filled with whatever box was nearest. Players who keep
bare shelves for layout had no way to stop that.

Default stays true: off by default would mean a newly bought shelf is never
stocked again."
```

---

## Task 2: Shared source folder

**Files:**
- Create: `Shared/Ui/GuiCaps.cs` (moved)
- Create: `Shared/Ui/Controls.cs` (moved)
- Create: `Shared/CursorControl.cs` (moved)
- Delete: `CheatForDev/Ui/GuiCaps.cs`, `CheatForDev/Ui/Controls.cs`, `CheatForDev/CursorControl.cs`
- Modify: `CheatForDev/CheatForDev.csproj`
- Modify: `SmartRestockEmployees/SmartRestockEmployees.csproj`

- [ ] **Step 1: Move the three files**

```bash
mkdir -p Shared/Ui Shared/Game
git mv CheatForDev/Ui/GuiCaps.cs Shared/Ui/GuiCaps.cs
git mv CheatForDev/Ui/Controls.cs Shared/Ui/Controls.cs
git mv CheatForDev/CursorControl.cs Shared/CursorControl.cs
```

- [ ] **Step 2: Renamespace them**

In all three moved files, change the namespace so both mods can use them:

- `Shared/Ui/GuiCaps.cs`: `namespace CheatForDev.Ui` becomes `namespace AnimeShopMods.Ui`
- `Shared/Ui/Controls.cs`: `namespace CheatForDev.Ui` becomes `namespace AnimeShopMods.Ui`
- `Shared/CursorControl.cs`: `namespace CheatForDev` becomes `namespace AnimeShopMods`

`Shared/CursorControl.cs` refers to `GameAccess.Ui`, which lives in CheatForDev. Break that dependency by taking the service as an argument. Replace the `Acquire` method's service lookup:

```csharp
        public static void Acquire(UIService ui)
        {
            if (_lease != null || _manualFallback) return;

            try
            {
                if (ui != null)
                {
                    _owner ??= new Il2CppSystem.Object();
                    _lease = ui.AcquireCursor(_owner);
                    if (_lease != null) return;
                }
            }
```

Update `Tick` and any other method in that file that called `Acquire()` to take and forward the same `UIService` argument. Remove the now-unused `using Il2CppProject.Code.Core.UI;` only if the compiler says it is unused — `UIService` and `ICursorLease` both come from it, so it almost certainly stays.

- [ ] **Step 3: Add the using directives in CheatForDev**

Every CheatForDev file that referenced `CheatForDev.Ui` or the moved `CursorControl` needs `using AnimeShopMods;` or `using AnimeShopMods.Ui;`. The compiler in step 5 will name each one. Update CheatForDev's `CursorControl.Acquire()` / `Tick()` call sites to pass `GameAccess.Ui`.

- [ ] **Step 4: Wire both projects**

`CheatForDev/CheatForDev.csproj` — add inside `<Project>`:

```xml
  <ItemGroup>
    <Compile Include="..\Shared\**\*.cs" />
  </ItemGroup>
```

`SmartRestockEmployees/SmartRestockEmployees.csproj` — the same block.

The SDK globs `**/*.cs` under each project folder by default; `Shared/` sits above both, so it must be included explicitly. It is compiled into each assembly — there is no third DLL for players to install, which is the whole point.

- [ ] **Step 5: Build**

Run the build command from the header.
Expected: `Build succeeded`. If it fails, it will be missing `using` directives from step 3 — add them and rebuild.

- [ ] **Step 6: In-game check — nothing changed**

Deploy both DLLs. Launch. Open CheatForDev with F8.
Expected: the cheat menu opens and behaves exactly as before. The IMGUI survey still prints to `Latest.log`. This task is a pure move; any visible difference is a bug.

- [ ] **Step 7: Commit**

```bash
git add -A Shared CheatForDev SmartRestockEmployees
git commit -m "Move shared UI source into Shared/

Both mods compile the same source rather than shipping a third DLL that
players would have to install. CursorControl now takes the UIService as an
argument so it no longer depends on CheatForDev's service locator."
```

---

## Task 3: Extend the capability probe

The panel needs three things `GuiCaps` has never checked. Find out before building on them.

**Files:**
- Modify: `Shared/Ui/GuiCaps.cs`

- [ ] **Step 1: Add the three properties**

In `Shared/Ui/GuiCaps.cs`, next to the existing `public static bool ScrollView { get; private set; }`:

```csharp
        public static bool CustomStyle { get; private set; }
        public static bool DrawTexture { get; private set; }
        public static Font GameFont { get; private set; }
```

- [ ] **Step 2: Probe them**

In `Survey()`, after the `ScrollView` check and before the `if (_logged) return;` line:

```csharp
            CustomStyle = Check(report, "new GUIStyle", () =>
            {
                var style = new GUIStyle(GUI.skin.button) { fontSize = 13 };
                style.normal.background = Texture2D.whiteTexture;
            });

            DrawTexture = Check(report, "GUI.DrawTexture", () =>
                GUI.DrawTexture(Scratch, Texture2D.whiteTexture));

            GameFont = FindFont(report);
```

- [ ] **Step 3: Add the font lookup**

Add to the same class, next to `Check`:

```csharp
        // IMGUI's built-in font is small and plain. The game ships nicer ones; borrowing one costs
        // nothing and the panel is readable either way if this returns null.
        private static Font FindFont(List<string> report)
        {
            try
            {
                var fonts = Resources.FindObjectsOfTypeAll<Font>();
                if (fonts == null || fonts.Length == 0)
                {
                    report.Add("FAIL  game Font -> none found");
                    return null;
                }

                foreach (var font in fonts)
                {
                    if (font == null || !font.dynamic) continue;
                    report.Add($"OK    game Font -> {font.name}");
                    return font;
                }

                report.Add("FAIL  game Font -> none dynamic");
                return null;
            }
            catch (Exception ex)
            {
                report.Add($"FAIL  game Font -> {ex.GetType().Name}");
                return null;
            }
        }
```

Only dynamic fonts are usable: a static bitmap font renders as blank boxes for any character outside the atlas it was baked with.

- [ ] **Step 4: Reset the font on re-survey**

In `Reset()`, add:

```csharp
            GameFont = null;
```

- [ ] **Step 5: Build**

Run the build command.
Expected: `Build succeeded`

- [ ] **Step 6: In-game check — read the survey**

Deploy CheatForDev, launch, load a save, press F8 to trigger the survey.
Expected: `Latest.log` contains an `IMGUI survey` block with three new lines. Record which pass:

```
OK    new GUIStyle
OK    GUI.DrawTexture
OK    game Font -> <some name>
```

If `new GUIStyle` fails, stop and report it — Task 4 is not buildable and the panel falls back to `Controls` styling throughout. If only the font fails, carry on; Task 4 handles a null font.

- [ ] **Step 7: Commit**

```bash
git add Shared/Ui/GuiCaps.cs
git commit -m "Probe custom GUIStyle, DrawTexture and game fonts

The panel needs all three. This build strips IMGUI unpredictably, so find
out at runtime instead of assuming."
```

---

## Task 4: The skin

**Files:**
- Create: `Shared/Ui/Skin.cs`

- [ ] **Step 1: Write the skin**

Create `Shared/Ui/Skin.cs`:

```csharp
using UnityEngine;

namespace AnimeShopMods.Ui
{
    // IMGUI ships one look: small grey Arial on a grey box. Everything here exists to get past
    // that. Textures are built in code rather than shipped as files so the mod stays one DLL.
    //
    // Colours follow the mod's MelonColor so the panel and the console banner match.
    internal static class Skin
    {
        public static readonly Color Panel = new Color32(0x1B, 0x1A, 0x22, 0xF5);
        public static readonly Color Raised = new Color32(0x24, 0x22, 0x2D, 0xFF);
        public static readonly Color Line = new Color32(0x3A, 0x37, 0x45, 0xFF);
        public static readonly Color Accent = new Color32(0xF5, 0x8C, 0xC4, 0xFF);
        public static readonly Color AccentText = new Color32(0x3A, 0x10, 0x29, 0xFF);
        public static readonly Color Text = new Color32(0xF3, 0xF1, 0xF7, 0xFF);
        public static readonly Color TextDim = new Color32(0x8C, 0x87, 0x99, 0xFF);
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

        private static Texture2D _panelTex;
        private static Texture2D _raisedTex;
        private static Texture2D _accentTex;
        private static Texture2D _accentHoverTex;
        private static Texture2D _outlineTex;
        private static Texture2D _outlineHoverTex;

        public static void Reset()
        {
            Ready = false;
        }

        // Called from OnGUI, which is the only place GUI.skin and style construction are legal.
        public static void EnsureBuilt()
        {
            if (Ready || !GuiCaps.CustomStyle) return;

            Pixel = Solid(Color.white);
            _panelTex = Rounded(Panel, Line, 6);
            _raisedTex = Rounded(Raised, Raised, 4);
            _accentTex = Rounded(Accent, Accent, 4);
            _accentHoverTex = Rounded(Lift(Accent, 0.12f), Lift(Accent, 0.12f), 4);
            _outlineTex = Rounded(new Color(0f, 0f, 0f, 0f), new Color32(0x56, 0x52, 0x6B, 0xFF), 4);
            _outlineHoverTex = Rounded(Raised, new Color32(0x56, 0x52, 0x6B, 0xFF), 4);

            PanelBox = Box(_panelTex, 8);
            RaisedBox = Box(_raisedTex, 6);

            Title = LabelStyle(15, Text);
            Body = LabelStyle(13, new Color32(0xE2, 0xDE, 0xEC, 0xFF));
            Hint = LabelStyle(12, TextDim);
            Hint.wordWrap = true;

            Primary = ButtonStyle(_accentTex, _accentHoverTex, AccentText, AccentText);
            Secondary = ButtonStyle(_outlineTex, _outlineHoverTex,
                new Color32(0xE2, 0xDE, 0xEC, 0xFF), Text);

            Ready = true;
        }

        private static GUIStyle Box(Texture2D texture, int border)
        {
            var style = new GUIStyle(GUI.skin.box)
            {
                border = new RectOffset(border, border, border, border),
                padding = new RectOffset(14, 14, 12, 12),
            };
            style.normal.background = texture;
            return style;
        }

        // Named LabelStyle, not Label: a method and the Text colour field cannot share a name, and
        // "Label" keeps it obvious which GUI.skin entry it derives from.
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
            texture.hideFlags = HideFlags.HideAndDontSave;
            return texture;
        }

        // A 9-slice tile: corners stay put, edges and middle stretch. Size is 2*radius+2 so the
        // one-pixel stretchable centre sits between the two corner bands.
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
            texture.hideFlags = HideFlags.HideAndDontSave;
            return texture;
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
```

`hideFlags = HideAndDontSave` keeps Unity from destroying these textures on a scene change, which would leave the panel drawing null textures after the player loads a save.

- [ ] **Step 2: Build**

Run the build command.
Expected: `Build succeeded`

- [ ] **Step 3: In-game check**

Nothing renders the skin yet, so verify it constructs without throwing. Add a temporary call at the top of `CheatWindow.Draw` (or wherever CheatForDev's `OnGUI` begins):

```csharp
            Skin.EnsureBuilt();
```

Deploy, launch, open the menu with F8.
Expected: no new exceptions in `Latest.log`. The cheat menu still draws. Remove the temporary call afterwards.

- [ ] **Step 4: Commit**

```bash
git add Shared/Ui/Skin.cs
git commit -m "Add a hand-built IMGUI skin

Textures are generated in code so the mod stays a single DLL. Rounded
corners come from a 9-slice tile; the accent matches the mod's MelonColor."
```

---

## Task 5: Shelf lookup and memory

**Files:**
- Create: `Shared/Game/ShelfFinder.cs`
- Create: `Shared/Game/ShelfMemory.cs`

- [ ] **Step 1: Write the finder**

Create `Shared/Game/ShelfFinder.cs`. This is `ShelfCheats.Scan`, `Describe` and `ShelfUnderCrosshair` with the CheatForDev-specific logging and service lookups taken out, plus the product icon the panel needs.

```csharp
using System;
using System.Collections.Generic;
using Il2CppProject.Code.Gameplay.Configs;
using Il2CppProject.Code.Gameplay.Interactions.Shelfs;
using Il2CppProject.Code.Gameplay.Player.Products;
using MelonLoader;
using UnityEngine;

namespace AnimeShopMods.Game
{
    // Finding shelves and saying what is on them, in terms a player would recognise:
    // the product's own name and icon, never a GameObject name.
    internal static class ShelfFinder
    {
        internal sealed class ShelfEntry
        {
            public ShelfProducts Shelf;
            public string ProductName;
            public Texture ProductIcon;
            public int ItemCount;
            public int MaxCount;
            public int UsedSlots;
            public int SlotCount;
        }

        public static List<ShelfEntry> Scan(ProductsConfig products)
        {
            var result = new List<ShelfEntry>();
            try
            {
                foreach (var shelf in UnityEngine.Object.FindObjectsOfType<ShelfProducts>())
                {
                    if (shelf == null) continue;
                    var entry = Describe(shelf, products);
                    if (entry != null) result.Add(entry);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[ShelfFinder] Scanning shelves failed: {ex.Message}");
            }
            result.Sort((a, b) => string.CompareOrdinal(a.ProductName, b.ProductName));
            return result;
        }

        public static ShelfEntry Describe(ShelfProducts shelf, ProductsConfig products)
        {
            if (shelf == null) return null;
            try
            {
                var places = shelf.Places;
                int slots = places == null ? 0 : places.Length;
                int items = 0;
                int max = 0;
                int used = 0;
                string name = null;
                Texture icon = null;

                for (int i = 0; i < slots; i++)
                {
                    var place = places[i];
                    var productPlace = place == null ? null : place.ProductPlace;
                    if (productPlace == null) continue;

                    items += productPlace.Count;
                    max += productPlace.MaxCount;
                    if (productPlace.Count > 0) used++;

                    if (name != null) continue;
                    name = ProductName(place, products);
                    icon = ProductIcon(place);
                }

                return new ShelfEntry
                {
                    Shelf = shelf,
                    ProductName = name ?? "Empty shelf",
                    ProductIcon = icon,
                    ItemCount = items,
                    MaxCount = max,
                    UsedSlots = used,
                    SlotCount = slots,
                };
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[ShelfFinder] Describing a shelf failed: {ex.Message}");
                return null;
            }
        }

        public static ShelfProducts UnderCrosshair(float maxDistance = 6f)
        {
            try
            {
                var camera = Camera.main;
                if (camera == null) return null;

                var ray = new Ray(camera.transform.position, camera.transform.forward);
                var hits = Physics.RaycastAll(ray, maxDistance);
                if (hits == null) return null;

                ShelfProducts nearest = null;
                float nearestDistance = float.MaxValue;
                for (int i = 0; i < hits.Length; i++)
                {
                    var hit = hits[i];
                    if (hit.collider == null) continue;
                    var shelf = hit.collider.GetComponentInParent<ShelfProducts>();
                    if (shelf == null) continue;
                    if (hit.distance >= nearestDistance) continue;
                    nearestDistance = hit.distance;
                    nearest = shelf;
                }
                return nearest;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[ShelfFinder] Looking for a shelf failed: {ex.Message}");
                return null;
            }
        }

        // The game's own look-highlight, the one the crosshair triggers. It has to be re-applied
        // every frame: the game's look raycast clears the flag on everything it is not pointing at.
        public static void Highlight(ShelfProducts shelf)
        {
            if (shelf == null) return;
            try
            {
                var places = shelf.Places;
                if (places == null) return;
                for (int i = 0; i < places.Length; i++)
                    if (places[i] != null) places[i].EnableLook(true);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[ShelfFinder] Highlighting a shelf failed: {ex.Message}");
            }
        }

        private static Texture ProductIcon(ProductPricePlace place)
        {
            try
            {
                var sprite = place._currentIcon;
                return sprite == null ? null : sprite.texture;
            }
            catch
            {
                return null;
            }
        }

        private static string ProductName(ProductPricePlace place, ProductsConfig products)
        {
            try
            {
                int definitionId = ShelfMemory.SlotProductId(place);
                if (definitionId <= 0) return null;
                var definition = products == null ? null : products.FindProductDefinition(definitionId);
                return definition == null ? null : definition.Name;
            }
            catch
            {
                return null;
            }
        }
    }
}
```

- [ ] **Step 2: Write the memory helper**

Create `Shared/Game/ShelfMemory.cs`:

```csharp
using System;
using Il2CppProject.Code.Gameplay.Interactions.Shelfs;
using Il2CppProject.Code.Gameplay.Player.Products;
using MelonLoader;

namespace AnimeShopMods.Game
{
    // A shelf slot remembers the last product it held, and that is what makes employees refill it
    // forever. Clearing the memory is the only way to hand a slot back to the player.
    //
    // LastDefinitionId is get-only, but Il2CppInterop exposes the backing field as settable.
    internal static class ShelfMemory
    {
        public static int SlotProductId(ProductPricePlace place)
        {
            if (place == null) return 0;

            var productPlace = place.ProductPlace;
            if (productPlace != null && productPlace.Count > 0)
            {
                var info = place.ProductInfo;
                if (info != null && info.DefinitionId > 0) return info.DefinitionId;
            }

            return place.LastDefinitionId;
        }

        public static bool Remembers(ProductPricePlace place)
        {
            return SlotProductId(place) > 0;
        }

        // Returns how many slots actually had something to forget.
        public static int Clear(ShelfProducts shelf)
        {
            if (shelf == null) return 0;

            int cleared = 0;
            try
            {
                var places = shelf.Places;
                if (places == null) return 0;

                for (int i = 0; i < places.Length; i++)
                {
                    var place = places[i];
                    if (place == null || place._lastDefinitionId == 0) continue;

                    place._lastDefinitionId = 0;
                    place.EnsureProductSaveDataServer(true);
                    cleared++;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[ShelfMemory] Clearing a shelf failed: {ex}");
            }
            return cleared;
        }
    }
}
```

- [ ] **Step 3: Build**

Run the build command.
Expected: `Build succeeded`

If `EnsureProductSaveDataServer` does not resolve, check the exact name against `get_type_members` for `ProductPricePlace` — it is a private method that Il2CppInterop publishes, and its name is stable but its arity is worth confirming.

- [ ] **Step 4: In-game check**

No UI calls this yet. Add a temporary hotkey in `SmartRestockEmployees/Main.cs` `OnUpdate`:

```csharp
            if (Input.GetKeyDown(KeyCode.F9))
            {
                var shelf = AnimeShopMods.Game.ShelfFinder.UnderCrosshair();
                MelonLogger.Msg($"[temp] cleared {AnimeShopMods.Game.ShelfMemory.Clear(shelf)} slot(s)");
            }
```

Deploy, launch. Put stock on a shelf, sell or remove it all so slots are empty but remembered. Confirm employees refill it. Then look at the shelf and press F9.

Expected: `Latest.log` shows `[temp] cleared N slot(s)` with N > 0, and employees stop refilling that shelf. Save, quit, reload.
Expected: the shelf is still forgotten. **If it is remembered again after reload, `EnsureProductSaveDataServer` is not enough** — record that and fall back to `RestoreStateServer` with a zeroed `ProductSaveData` before continuing.

Remove the temporary hotkey afterwards.

- [ ] **Step 5: Commit**

```bash
git add Shared/Game/ShelfFinder.cs Shared/Game/ShelfMemory.cs
git commit -m "Add shelf lookup and memory clearing

Shelves are described by product name and icon, never GameObject name --
players have no idea what Shelf_03 is. Highlight() reuses the game's own
look outline instead of drawing one."
```

---

## Task 6: Returning stock to a delivery zone

**Files:**
- Create: `Shared/Game/DeliveryReturn.cs`

- [ ] **Step 1: Write it**

Create `Shared/Game/DeliveryReturn.cs`:

```csharp
using System;
using Il2CppProject.Code.Gameplay.Configs;
using Il2CppProject.Code.Gameplay.Controllers;
using Il2CppProject.Code.Gameplay.Interactions.Shelfs;
using Il2CppProject.Code.Gameplay.Player.Products;
using MelonLoader;
using UnityEngine;

namespace AnimeShopMods.Game
{
    // Turning a shelf's stock back into boxes at a delivery zone.
    //
    // Zone 1 goes through the game's own TryGetRecoveryPose. Zone 2 has no API at all, so the pose
    // is built by hand from OrderController._secondaryPoints. BuildSpawnAnchors/TryGetSpawnPose
    // would be tidier but take a SpawnAnchor struct, and struct marshalling through Il2CppInterop
    // is what broke SetCountServer in this codebase -- not a risk worth taking for neatness.
    internal static class DeliveryReturn
    {
        internal struct Result
        {
            public int Returned;
            public int Skipped;
            public bool ZoneWasFull;
            public bool FellBackToZoneOne;
        }

        public static Result Run(ShelfProducts shelf, OrderController orders, ProductsConfig products,
            bool secondZone)
        {
            var result = default(Result);
            if (shelf == null || orders == null || products == null)
            {
                MelonLogger.Warning("[DeliveryReturn] Missing shelf, order controller or product config.");
                return result;
            }

            if (secondZone && !HasSecondZone(orders))
            {
                secondZone = false;
                result.FellBackToZoneOne = true;
            }

            try
            {
                var places = shelf.Places;
                if (places == null) return result;

                int stackIndex = 0;
                for (int i = 0; i < places.Length; i++)
                {
                    var place = places[i];
                    var productPlace = place == null ? null : place.ProductPlace;
                    if (productPlace == null || productPlace.Count == 0) continue;

                    int count = productPlace.Count;
                    var productId = productPlace.Id;
                    int definitionId = ShelfMemory.SlotProductId(place);

                    var pickupDefinition = definitionId <= 0
                        ? null
                        : products.FindPickupDefinitionByProductId(definitionId);

                    if (pickupDefinition == null)
                    {
                        result.Skipped += count;
                        continue;
                    }

                    if (!TryPose(orders, secondZone, stackIndex, definitionId,
                            out var position, out var rotation))
                    {
                        result.ZoneWasFull = true;
                        break;
                    }

                    Empty(productPlace);
                    orders.CreateShelfPickup(pickupDefinition.Id, position, rotation, count,
                        productId, null, null, null);

                    result.Returned += count;
                    stackIndex++;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[DeliveryReturn] Returning a shelf failed: {ex}");
            }
            return result;
        }

        public static bool HasSecondZone(OrderController orders)
        {
            try
            {
                var points = orders._secondaryPoints;
                return points != null && points.Count > 0;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryPose(OrderController orders, bool secondZone, int stackIndex,
            int definitionId, out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;

            if (!secondZone)
                return orders.TryGetRecoveryPose(out position, out rotation, true);

            var points = orders._secondaryPoints;
            if (points == null || points.Count == 0) return false;

            var anchor = points[stackIndex % points.Count];
            var transform = anchor == null ? null : anchor.transform;
            if (transform == null) return false;

            float step = orders.DefaultSpawnStackHeight + orders.SpawnStackClearance;
            int layer = stackIndex / points.Count;

            position = transform.position + Vector3.up * (step * layer);
            rotation = transform.rotation;
            orders.NormalizeSpawnPose(ref position, ref rotation, definitionId);
            return true;
        }

        // One item at a time. SetCountServer(0) looks simpler but cannot be called from here: its
        // ProductTransferPayload parameter is a struct, and the Il2CppInterop stub unboxes the null
        // default before the game is ever reached. RemoveLastItemServer is the path a customer
        // taking the last item goes down. The loop is bounded by the starting count, so it does not
        // depend on Count updating synchronously.
        private static void Empty(ProductPlace productPlace)
        {
            int start = productPlace.Count;
            for (int n = 0; n < start; n++)
                productPlace.RemoveLastItemServer();
        }
    }
}
```

The zone-1 path is deliberately left on `TryGetRecoveryPose`. It is the only part of this already proven to work in game, and it keeps its own stacking index inside the controller.

- [ ] **Step 2: Build**

Run the build command.
Expected: `Build succeeded`

`NormalizeSpawnPose` takes `Vector3&` and `Quaternion&`; Il2CppInterop surfaces those as `ref`. If the compiler disagrees, check the signature with `get_type_members` on `OrderController` before changing the call.

- [ ] **Step 3: In-game check — zone 1**

Temporary hotkey in `SmartRestockEmployees/Main.cs` `OnUpdate`:

```csharp
            if (Input.GetKeyDown(KeyCode.F9))
            {
                var shelf = AnimeShopMods.Game.ShelfFinder.UnderCrosshair();
                var result = AnimeShopMods.Game.DeliveryReturn.Run(shelf, Orders, Products, false);
                MelonLogger.Msg($"[temp] returned {result.Returned}, skipped {result.Skipped}, full {result.ZoneWasFull}");
            }
```

You need `Orders` and `Products` lookups in this mod. Add them in Task 7; for now use `UnityEngine.Object.FindObjectOfType<OrderController>()` and reach the config through it.

Deploy. Stock a shelf. Look at it, press F9.
Expected: boxes appear at the first delivery zone, the shelf is bare, the log reports the item count.

- [ ] **Step 4: In-game check — zone 2**

Change the temporary call's last argument to `true`. Rebuild, deploy, repeat on an unlocked second zone.
Expected: boxes appear at the *second* zone, standing on the floor, not clipping into it or floating. Stack several shelves' worth to confirm the layering offset works.

- [ ] **Step 5: In-game check — zone 2 locked**

Load a save early enough that the second zone is not unlocked.
Expected: boxes appear at zone 1 and `FellBackToZoneOne` is true. No exception.

Remove the temporary hotkey afterwards.

- [ ] **Step 6: Commit**

```bash
git add Shared/Game/DeliveryReturn.cs
git commit -m "Return shelf stock to a chosen delivery zone

Zone 1 keeps the game's own TryGetRecoveryPose. Zone 2 has no API, so the
pose is built from _secondaryPoints and normalised by the game. Falls back
to zone 1 when the second zone is not unlocked yet."
```

---

## Task 7: Mod plumbing — preferences, hotkey, cursor, OnGUI

**Files:**
- Modify: `SmartRestockEmployees/Main.cs`
- Create: `SmartRestockEmployees/GameLinks.cs`

- [ ] **Step 1: Add the lookups**

Create `SmartRestockEmployees/GameLinks.cs`:

```csharp
using System;
using Il2CppProject.Code.Core.Services;
using Il2CppProject.Code.Core.UI;
using Il2CppProject.Code.Gameplay.Configs;
using Il2CppProject.Code.Gameplay.Controllers;
using MelonLoader;
using UnityEngine;

namespace SmartRestockEmployees
{
    // The handful of game objects the panel needs. Service locator first, scene search second:
    // Il2CppInterop cannot always call a generic like AllServices.Get<T>(), so the fallback matters.
    internal static class GameLinks
    {
        private static bool _locatorBroken;
        private static OrderController _orders;
        private static TimeController _time;
        private static ConfigsService _configs;
        private static UIService _ui;

        public static void Reset()
        {
            _orders = null;
            _time = null;
            _configs = null;
            _ui = null;
        }

        public static OrderController Orders => Cached(ref _orders);
        public static TimeController Time => Cached(ref _time);

        public static ProductsConfig Products
        {
            get
            {
                var configs = Configs;
                return configs == null ? null : configs.Products;
            }
        }

        public static UIService Ui
        {
            get
            {
                if (_ui == null) _ui = Service<UIService>();
                return _ui;
            }
        }

        public static bool InGame => Time != null;

        public static bool IsServer
        {
            get
            {
                try
                {
                    var controller = Time;
                    return controller != null && controller.IsServerInitialized;
                }
                catch
                {
                    return false;
                }
            }
        }

        private static ConfigsService Configs
        {
            get
            {
                if (_configs == null) _configs = Service<ConfigsService>();
                return _configs;
            }
        }

        private static T Service<T>() where T : Il2CppSystem.Object
        {
            if (_locatorBroken) return null;
            try
            {
                return AllServices.Get<T>();
            }
            catch (Exception ex)
            {
                _locatorBroken = true;
                MelonLogger.Warning($"[SmartRestock] AllServices.Get<{typeof(T).Name}>() is unusable here " +
                                    $"({ex.GetType().Name}); falling back to scene lookups.");
                return null;
            }
        }

        private static T Cached<T>(ref T slot) where T : Component
        {
            if (slot != null) return slot;
            try
            {
                slot = UnityEngine.Object.FindObjectOfType<T>();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[SmartRestock] Could not find {typeof(T).Name}: {ex.Message}");
            }
            return slot;
        }
    }
}
```

- [ ] **Step 2: Add the hotkey preference**

In `SmartRestockEmployees/Main.cs`, add fields next to the others:

```csharp
        private static MelonPreferences_Entry<string> _panelKey;

        public static KeyCode PanelKey { get; private set; } = KeyCode.F7;
        public static bool PanelVisible { get; private set; }
```

Add the entry at the end of `OnInitializeMelon`, before the load message:

```csharp
            _panelKey = category.CreateEntry("PanelKey", "F7", "Panel key",
                "Key that opens the shelf panel. Any UnityEngine.KeyCode name.");

            if (!string.IsNullOrEmpty(_panelKey.Value) &&
                System.Enum.TryParse<KeyCode>(_panelKey.Value, true, out var parsed))
                PanelKey = parsed;
```

F7, not F8 — CheatForDev owns F8 and players may run both.

Change the load message to mention it:

```csharp
            MelonLogger.Msg($"{ModInfo.Name} {ModInfo.Version} loaded. Press {PanelKey} for the shelf panel.");
```

- [ ] **Step 3: Wire the panel into the loop**

Replace `OnUpdate` in `SmartRestockEmployees/Main.cs`:

```csharp
        public override void OnUpdate()
        {
            if (StuckWatchdogEnabled)
                StuckWatchdog.Tick();

            if (Input.GetKeyDown(PanelKey))
                SetPanelVisible(!PanelVisible);

            if (!PanelVisible) return;

            if (!GameLinks.InGame) SetPanelVisible(false);
            else CursorControl.Tick(GameLinks.Ui);
        }

        private static void SetPanelVisible(bool visible)
        {
            PanelVisible = visible;
            if (visible) CursorControl.Acquire(GameLinks.Ui);
            else CursorControl.Release();
            ShelfPanel.Reset();
        }

        public override void OnGUI()
        {
            if (!PanelVisible) return;

            if (!GuiCaps.Surveyed)
            {
                GuiCaps.Survey();
                return;
            }

            Skin.EnsureBuilt();
            ShelfPanel.Draw();
        }
```

Add the using directives at the top of the file:

```csharp
using AnimeShopMods;
using AnimeShopMods.Ui;
using SmartRestockEmployees.Ui;
using UnityEngine;
```

The survey owns its whole frame and draws nothing else. IMGUI matches controls between the layout and repaint passes and throws if the set differs, so probing and drawing must never share a frame.

- [ ] **Step 4: Reset on scene change**

In `OnSceneWasUnloaded`, add:

```csharp
            GameLinks.Reset();
            GuiCaps.Reset();
            Skin.Reset();
            ShelfPanel.Reset();
            SetPanelVisible(false);
```

- [ ] **Step 5: Build**

This will fail — `ShelfPanel` does not exist yet. That is expected; Task 8 creates it. To check the rest compiles, stub it first:

```csharp
// SmartRestockEmployees/Ui/ShelfPanel.cs
namespace SmartRestockEmployees.Ui
{
    internal static class ShelfPanel
    {
        public static void Reset() { }
        public static void Draw() { }
    }
}
```

Run the build command.
Expected: `Build succeeded`

- [ ] **Step 6: In-game check**

Deploy, launch, load a save, press F7.
Expected: the mouse cursor appears and gameplay input is released. The `IMGUI survey` block appears in `Latest.log` once. Press F7 again — the cursor is recaptured. Nothing is drawn yet.

- [ ] **Step 7: Commit**

```bash
git add SmartRestockEmployees/GameLinks.cs SmartRestockEmployees/Main.cs SmartRestockEmployees/Ui/ShelfPanel.cs
git commit -m "Add the panel hotkey, cursor handling and game lookups

F7, because CheatForDev already owns F8 and players may run both. The
capability survey owns its own frame -- IMGUI throws if the control set
differs between the layout and repaint passes."
```

---

## Task 8: The panel

**Files:**
- Modify: `SmartRestockEmployees/Ui/ShelfPanel.cs`

- [ ] **Step 1: Write the panel**

Replace the stub in `SmartRestockEmployees/Ui/ShelfPanel.cs`:

```csharp
using System;
using System.Collections.Generic;
using AnimeShopMods.Game;
using AnimeShopMods.Ui;
using Il2CppProject.Code.Gameplay.Interactions.Shelfs;
using MelonLoader;
using UnityEngine;

namespace SmartRestockEmployees.Ui
{
    // The player-facing panel. Two views: whichever shelf the player is looking at, and a list of
    // every shelf in the store. No engine vocabulary reaches the screen -- no slots, no definition
    // ids, no GameObject names.
    //
    // No GUILayout.Window: it needs a GUI.WindowFunction delegate and Il2Cpp marshalling of those is
    // unreliable. A plain area inside a box gives the same panel.
    internal static class ShelfPanel
    {
        private const float Width = 420f;
        private const float RowHeight = 44f;

        private static Rect _area = new Rect(40f, 60f, Width, 520f);
        private static Vector2 _scroll;
        private static bool _listView;

        private static List<ShelfFinder.ShelfEntry> _shelves;
        private static float _nextScan;

        // The shelf the player picked out of the list. Null means "whatever I am looking at".
        private static ShelfProducts _pinned;

        private static ShelfProducts _pending;
        private static bool _pendingSecondZone;
        private static int _pendingItems;
        private static string _status;

        public static void Reset()
        {
            _shelves = null;
            _nextScan = 0f;
            _pinned = null;
            _pending = null;
            _status = null;
            _scroll = Vector2.zero;
        }

        public static void Draw()
        {
            if (!Skin.Ready)
            {
                DrawFallback();
                return;
            }

            GUILayout.BeginArea(_area, Skin.PanelBox);
            try
            {
                Header();

                if (_pending != null) Confirmation();
                else if (_listView) ListView();
                else AimView();

                Footer();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[ShelfPanel] Draw failed: {ex}");
            }
            finally
            {
                GUILayout.EndArea();
            }
        }

        private static void Header()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(_listView ? "All shelves" : "Shelf", Skin.Title);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(_listView ? "Look instead" : "See all", Skin.Secondary, GUILayout.Width(110f)))
            {
                _listView = !_listView;
                _pinned = null;
                _status = null;
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(10f);
        }

        private static void AimView()
        {
            var shelf = _pinned != null ? _pinned : ShelfFinder.UnderCrosshair();
            if (shelf == null)
            {
                GUILayout.Label("Walk up to a shelf and look at it.", Skin.Hint);
                return;
            }

            var entry = ShelfFinder.Describe(shelf, GameLinks.Products);
            if (entry == null) return;

            // A shelf picked from the list stays put while the player reads the panel, and keeps
            // glowing so they can see which one it is.
            if (_pinned != null)
            {
                ShelfFinder.Highlight(_pinned);
                GUILayout.BeginHorizontal();
                GUILayout.Label("Picked from the list", Skin.Hint);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Unpin", Skin.Secondary, GUILayout.Width(80f))) _pinned = null;
                GUILayout.EndHorizontal();
                GUILayout.Space(6f);
            }

            ShelfCard(entry);
            GUILayout.Space(12f);
            Actions(entry);
        }

        private static void ListView()
        {
            if (_shelves == null || Time.realtimeSinceStartup > _nextScan)
            {
                _shelves = ShelfFinder.Scan(GameLinks.Products);
                _nextScan = Time.realtimeSinceStartup + 2f;
            }

            if (_shelves.Count == 0)
            {
                GUILayout.Label("No shelves in the store yet.", Skin.Hint);
                return;
            }

            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(300f));
            for (int i = 0; i < _shelves.Count; i++)
            {
                var entry = _shelves[i];
                GUILayout.BeginHorizontal(Skin.RaisedBox, GUILayout.Height(RowHeight));

                Icon(entry, 28f);
                GUILayout.BeginVertical();
                GUILayout.Label(entry.ProductName, Skin.Body);
                GUILayout.Label($"{entry.ItemCount} of {entry.MaxCount} items", Skin.Hint);
                GUILayout.EndVertical();

                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Open", Skin.Secondary, GUILayout.Width(74f)))
                {
                    _pinned = entry.Shelf;
                    _listView = false;
                    _status = null;
                }
                GUILayout.EndHorizontal();

                // Hovering a row lights up the real shelf. Shelves have no names a player would
                // recognise, so this is how they tell one row from another.
                if (Event.current != null && Event.current.type == EventType.Repaint &&
                    GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
                    ShelfFinder.Highlight(entry.Shelf);

                GUILayout.Space(4f);
            }
            GUILayout.EndScrollView();
        }

        private static void ShelfCard(ShelfFinder.ShelfEntry entry)
        {
            GUILayout.BeginHorizontal(Skin.RaisedBox);
            Icon(entry, 40f);
            GUILayout.BeginVertical();
            GUILayout.Label(entry.ProductName, Skin.Body);
            GUILayout.Label($"{entry.ItemCount} of {entry.MaxCount} items", Skin.Hint);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        private static void Actions(ShelfFinder.ShelfEntry entry)
        {
            if (!GameLinks.IsServer)
            {
                GUILayout.Label("Only the host can change shelves.", Skin.Hint);
                return;
            }

            GUILayout.Label("Empty this shelf", Skin.Hint);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Delivery 1", Skin.Primary)) Ask(entry, false);
            GUILayout.Space(8f);
            bool hasSecond = DeliveryReturn.HasSecondZone(GameLinks.Orders);
            if (GUILayout.Button("Delivery 2", hasSecond ? Skin.Primary : Skin.Secondary)) Ask(entry, true);
            GUILayout.EndHorizontal();

            GUILayout.Label(hasSecond
                ? "Items go back into boxes at the zone you pick, and employees stop refilling this shelf."
                : "The second delivery zone is not open yet. Items will go to Delivery 1.", Skin.Hint);

            GUILayout.Space(12f);
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();
            GUILayout.Label("Keep this shelf empty", Skin.Body);
            GUILayout.Label("Forget what used to be here", Skin.Hint);
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Forget", Skin.Secondary, GUILayout.Width(96f)))
            {
                int cleared = ShelfMemory.Clear(entry.Shelf);
                _status = cleared > 0
                    ? "Employees will leave this shelf alone."
                    : "This shelf was already forgotten.";
            }
            GUILayout.EndHorizontal();
        }

        private static void Ask(ShelfFinder.ShelfEntry entry, bool secondZone)
        {
            if (entry.ItemCount == 0)
            {
                int cleared = ShelfMemory.Clear(entry.Shelf);
                _status = cleared > 0
                    ? "Nothing to pack up. Employees will leave this shelf alone."
                    : "This shelf is already empty and forgotten.";
                return;
            }

            _pending = entry.Shelf;
            _pendingSecondZone = secondZone;
            _pendingItems = entry.ItemCount;
            _status = null;
        }

        private static void Confirmation()
        {
            bool second = _pendingSecondZone && DeliveryReturn.HasSecondZone(GameLinks.Orders);
            GUILayout.Label("Empty this shelf?", Skin.Title);
            GUILayout.Label($"{_pendingItems} items go back into boxes at Delivery {(second ? 2 : 1)}.",
                Skin.Hint);
            GUILayout.Space(12f);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Cancel", Skin.Secondary)) _pending = null;
            GUILayout.Space(8f);
            if (GUILayout.Button("Empty it", Skin.Primary))
            {
                var result = DeliveryReturn.Run(_pending, GameLinks.Orders, GameLinks.Products,
                    _pendingSecondZone);
                ShelfMemory.Clear(_pending);
                _status = Describe(result);
                _pending = null;
                _shelves = null;
            }
            GUILayout.EndHorizontal();
        }

        private static string Describe(DeliveryReturn.Result result)
        {
            if (result.ZoneWasFull && result.Returned == 0)
                return "The delivery zone is full. Nothing was moved.";

            string text = $"Packed up {result.Returned} items.";
            if (result.FellBackToZoneOne) text += " The second zone is not open yet, so they went to Delivery 1.";
            if (result.ZoneWasFull) text += " The zone filled up, so the rest stayed on the shelf.";
            if (result.Skipped > 0) text += $" {result.Skipped} items have no box to go back into and stayed put.";
            return text;
        }

        private static void Footer()
        {
            GUILayout.FlexibleSpace();

            if (!string.IsNullOrEmpty(_status))
            {
                GUILayout.Label(_status, Skin.Hint);
                GUILayout.Space(8f);
            }

            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();
            GUILayout.Label("Use never-filled slots", Skin.Body);
            GUILayout.Label("Applies to every shelf in the store", Skin.Hint);
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(Main.FillFreeSlots ? "On" : "Off", Skin.Secondary, GUILayout.Width(68f)))
                Main.SetFillFreeSlots(!Main.FillFreeSlots);
            GUILayout.EndHorizontal();
        }

        private static void Icon(ShelfFinder.ShelfEntry entry, float size)
        {
            var rect = GUILayoutUtility.GetRect(size, size, GUILayout.Width(size), GUILayout.Height(size));
            if (!GuiCaps.DrawTexture) return;

            if (entry.ProductIcon != null) GUI.DrawTexture(rect, entry.ProductIcon);
            else GUI.DrawTexture(rect, Skin.Pixel);
            GUILayout.Space(10f);
        }

        // Every styled control degrades to Controls, which already knows how to survive a stripped
        // IMGUI. If the skin could not be built at all, this is what the player sees.
        private static void DrawFallback()
        {
            GUILayout.BeginArea(_area);
            try
            {
                Controls.Header("Shelf");
                var shelf = ShelfFinder.UnderCrosshair();
                if (shelf == null)
                {
                    Controls.Label("Walk up to a shelf and look at it.");
                    return;
                }

                var entry = ShelfFinder.Describe(shelf, GameLinks.Products);
                if (entry == null) return;

                Controls.Label($"{entry.ProductName} - {entry.ItemCount} of {entry.MaxCount} items");
                if (Controls.Button("Empty to Delivery 1", 180f)) Ask(entry, false);
                if (Controls.Button("Empty to Delivery 2", 180f)) Ask(entry, true);
                if (Controls.Button("Forget this shelf", 180f)) ShelfMemory.Clear(entry.Shelf);
                if (!string.IsNullOrEmpty(_status)) Controls.Label(_status);
            }
            finally
            {
                GUILayout.EndArea();
            }
        }
    }
}
```

- [ ] **Step 2: Add the toggle setter**

The footer writes the preference back. In `SmartRestockEmployees/Main.cs`:

```csharp
        public static void SetFillFreeSlots(bool value)
        {
            if (_fillFreeSlots == null) return;
            _fillFreeSlots.Value = value;
            MelonPreferences.Save();
        }
```

- [ ] **Step 3: Build**

Run the build command.
Expected: `Build succeeded`

- [ ] **Step 4: In-game check — aim view**

Deploy, launch, load a save. Look at a stocked shelf, press F7.
Expected: a dark rounded panel with the product's real icon and name, the item count, two pink Delivery buttons, a Forget button, and the on/off toggle at the bottom. Text is readable and nothing overlaps.

- [ ] **Step 5: In-game check — the actions**

Press Delivery 1.
Expected: a confirmation naming the item count and the zone. Press Cancel — nothing happens. Press it again, then Empty it.
Expected: boxes appear at the zone, the shelf is bare, the panel reports how many items moved, and employees do not restock that shelf.

- [ ] **Step 6: In-game check — list view**

Press "See all".
Expected: a scrollable list of shelves by product name and count. Moving the mouse down the rows lights up each matching shelf in the world, one at a time. Press "Open" on a row.
Expected: the panel returns to the shelf view showing *that* shelf, it keeps glowing while the panel is open, and the actions work on it even when the player is looking elsewhere. "Unpin" returns to following the crosshair.

- [ ] **Step 7: In-game check — the toggle**

Press the On/Off control in the footer.
Expected: the label flips, and `FillFreeSlots` in `<GamePath>/UserData/MelonPreferences.cfg` changes to match. Employees change behaviour on the next restock run without a restart.

- [ ] **Step 8: Commit**

```bash
git add SmartRestockEmployees/Ui/ShelfPanel.cs SmartRestockEmployees/Main.cs
git commit -m "Add the shelf panel

Aim at a shelf or pick it from the list; hovering a row lights up the real
shelf, since shelves have no name a player would recognise. Destructive
actions confirm with the item count first, and every action reports a
number back into the panel rather than only into the log."
```

---

## Task 9: Release

**Files:**
- Modify: `SmartRestockEmployees/Main.cs` (version)
- Modify: `SmartRestockEmployees/Publish/manifest.json`
- Modify: `SmartRestockEmployees/Publish/README.md`
- Modify: `README.md`

- [ ] **Step 1: Bump the version**

In `SmartRestockEmployees/Main.cs`, `ModInfo.Version` becomes `"1.4.0"`.

In `SmartRestockEmployees/Publish/manifest.json`, `version_number` becomes `"1.4.0"`. `pack.ps1` fails the build if these two disagree, which is the point of it.

- [ ] **Step 2: Document the release**

Add to `SmartRestockEmployees/Publish/README.md`, in the style of the existing release sections:

```markdown
## 1.4.0

Two things people asked for on Nexus, both about shelves you want to keep empty.

**Press F7 while looking at a shelf.** The panel tells you what is on it and gives you two
buttons: pack the stock back into boxes at a delivery zone of your choice, or just forget the
shelf so employees stop refilling it. "See all" lists every shelf in the store; moving the mouse
down the list lights up the matching shelf so you can tell which is which.

**New setting: Fill free slots.** On by default, which is how the mod has always behaved. Turn it
off and employees will only touch slots that have held something before, so a bare shelf stays
bare until you stock it yourself.

| Setting | Default | What it does |
| --- | --- | --- |
| `FillFreeSlots` | `true` | Let employees use slots that have never held anything. |
| `PanelKey` | `F7` | Opens the shelf panel. Any UnityEngine KeyCode name. |
```

Update the configuration table further up the same file with the two new entries.

- [ ] **Step 3: Update the repo readme**

In the root `README.md`, update the Smart Restock Employees entry to mention the panel and the new version.

- [ ] **Step 4: Pack**

```bash
powershell -ExecutionPolicy Bypass -File tools/pack.ps1 -Mod SmartRestockEmployees
```

Expected: `Created ...\SmartRestockEmployees-1.4.0-Nexus.zip`. If it throws a version mismatch, one of step 1's two files was missed.

- [ ] **Step 5: Final in-game check on the packed build**

Install the zip's `SmartRestockEmployees.dll` into a clean `Mods` folder with no other mods present, on a save that has never seen this mod.
Expected: the mod loads, the log line names F7, the panel opens, and the store runs a full day without an exception in `Latest.log`.

- [ ] **Step 6: Commit**

```bash
git add SmartRestockEmployees/Main.cs SmartRestockEmployees/Publish/manifest.json SmartRestockEmployees/Publish/README.md README.md
git commit -m "Release Smart Restock Employees 1.4.0"
```

---

## Self-review notes

Spec coverage checked section by section:

| Spec section | Task |
| --- | --- |
| Restocking rule | 1 |
| Clearing a shelf's memory | 5 |
| Returning stock to a delivery zone | 6 |
| The panel — aim view | 8 |
| The panel — list view | 8 |
| The panel — wording | 8 |
| Code layout (`Shared/`) | 2 |
| Failure handling | 6 (zone full, no box), 8 (not host, zone 2 locked), all (try/catch) |
| Capability probe | 3 |
| Testing checklist | 1, 5, 6, 8, 9 |

Two spec checklist items are worth calling out because they span tasks:

- **"Panel renders with each probed capability forced off"** — exercise this by editing `GuiCaps` to hard-code `CustomStyle = false`, rebuilding, and confirming `DrawFallback` renders. Do it during Task 8 step 4.
- **"Join as a client"** — needs a second machine or a second Steam account. If that is not available, verify the guard by temporarily returning `false` from `GameLinks.IsServer` and confirming the panel says so instead of throwing.

using AnimeShopMods;
using AnimeShopMods.Ui;
using System;
using System.Collections.Generic;
using CheatForDev.Cheats;
using Il2CppProject.Code.Gameplay.AI.Buyer;
using Il2CppProject.Code.Gameplay.Configs;
using Il2CppProject.Code.Gameplay.Controllers;
using MelonLoader;
using UnityEngine;

namespace CheatForDev.Ui
{
    // Immediate-mode menu. Two deliberate choices, both forced by how this game was built:
    //   - no GUILayout.Window, because that needs a GUI.WindowFunction delegate and Il2Cpp marshalling
    //     of those is unreliable; a plain area inside a box gives the same panel;
    //   - no typed input, because GUILayout.TextField is stripped out of the build. Numbers are entered
    //     with stepper and preset buttons through Controls, which degrade to whatever survived.
    internal static class CheatWindow
    {
        private static readonly string[] TabNames =
            { "Money", "Staff", "Time", "Shelves", "Orders", "Testing", "Unlocks" };

        private const float TitleBarHeight = 26f;
        private static Rect _area = new Rect(24f, 24f, 660f, 680f);

        private static bool _dragging;
        private static Vector2 _dragGrip;

        private static int _tab;
        private static Vector2 _scroll;
        private static readonly HashSet<string> _reportedFailures = new HashSet<string>();

        // Money and progression
        private static readonly float[] MoneySteps = { 1_000f, 10_000f, 100_000f, 1_000_000f };
        private static readonly float[] SmallSteps = { 1f, 10f, 100f };
        private static float _infiniteTarget = 1_000_000f;

        // Time
        private static int _wantedHour = 12;
        private static int _wantedMinute = 0;

        // Shelves
        private static List<ShelfCheats.ShelfEntry> _shelves;
        private static readonly HashSet<int> _selectedShelves = new HashSet<int>();
        private static bool _aimMode;

        // Orders
        private static int _selectedBoxId = -1;
        private static string _selectedBoxLabel = "(none)";
        private static int _catalogPage;

        // The frame the capability survey owns. Nothing else is drawn during it.
        private static int _surveyFrame = -1;

        public static Vector2 Position => new Vector2(_area.x, _area.y);

        public static void MoveTo(float x, float y)
        {
            _area.x = x;
            _area.y = y;
            ClampToScreen();
        }

        // Dragging by the title bar, done by hand. GUILayout.Window would give this for free but needs a
        // GUI.WindowFunction delegate, and Il2Cpp marshalling of those is exactly what this menu avoids.
        private static void HandleDrag()
        {
            if (!GuiCaps.Events) return;

            var e = Event.current;
            if (e == null) return;

            var titleBar = new Rect(_area.x, _area.y, _area.width, TitleBarHeight);

            switch (e.type)
            {
                case EventType.MouseDown:
                    if (e.button != 0 || !titleBar.Contains(e.mousePosition)) break;
                    _dragging = true;
                    _dragGrip = e.mousePosition - new Vector2(_area.x, _area.y);
                    e.Use();
                    break;

                case EventType.MouseDrag:
                    if (!_dragging) break;
                    _area.x = e.mousePosition.x - _dragGrip.x;
                    _area.y = e.mousePosition.y - _dragGrip.y;
                    ClampToScreen();
                    e.Use();
                    break;

                case EventType.MouseUp:
                    if (!_dragging) break;
                    _dragging = false;
                    ClampToScreen();
                    Main.SaveWindowPosition(_area.x, _area.y);
                    e.Use();
                    break;
            }
        }

        // Never let the bar leave the screen, or there is no way to grab it back.
        private static void ClampToScreen()
        {
            // At mod-init time the screen size can still be zero; clamping against that would throw the
            // window off to the left before it is ever shown.
            if (Screen.width <= 0 || Screen.height <= 0) return;

            float maxX = Mathf.Max(0f, Screen.width - 80f);
            float maxY = Mathf.Max(0f, Screen.height - TitleBarHeight);
            _area.x = Mathf.Clamp(_area.x, 80f - _area.width, maxX);
            _area.y = Mathf.Clamp(_area.y, 0f, maxY);
        }

        public static void Reset()
        {
            _shelves = null;
            _selectedShelves.Clear();
            _aimMode = false;
            _catalogPage = 0;
            _licenses = null;
            _reportedFailures.Clear();
            _surveyFrame = -1;
            _dragging = false;
            OrderCheats.InvalidateCatalog();
        }

        public static void Draw()
        {
            if (_surveyFrame < 0) _surveyFrame = Time.frameCount;
            if (Time.frameCount == _surveyFrame)
            {
                GuiCaps.Survey();
                return;   // the survey owns this frame; the menu starts on the next one
            }

            HandleDrag();

            if (GuiCaps.Box)
                GUI.Box(_area, $"{ModInfo.Name} {ModInfo.Version}   -   drag this bar   -   {Main.ToggleKey} to close");

            GUILayout.BeginArea(new Rect(_area.x + 10f, _area.y + 26f, _area.width - 20f, _area.height - 36f));
            try
            {
                DrawStatusLine();
                _tab = Controls.Tabs(_tab, TabNames);
                Controls.Space();

                _scroll = Controls.BeginScroll(_scroll);
                try
                {
                    DrawTab(_tab);
                }
                finally
                {
                    Controls.EndScroll();
                }
            }
            finally
            {
                GUILayout.EndArea();
            }
        }

        private static void DrawTab(int tab)
        {
            try
            {
                switch (tab)
                {
                    case 0: DrawMoneyTab(); break;
                    case 1: DrawStaffTab(); break;
                    case 2: DrawTimeTab(); break;
                    case 3: DrawShelvesTab(); break;
                    case 4: DrawOrdersTab(); break;
                    case 5: DrawTestingTab(); break;
                    default: DrawUnlocksTab(); break;
                }
            }
            catch (Exception ex)
            {
                // Log once per tab: OnGUI runs several times a frame and would otherwise flood the console.
                string key = $"{TabNames[tab]}:{ex.GetType().Name}";
                if (_reportedFailures.Add(key))
                    MelonLogger.Error($"[CheatForDev] The {TabNames[tab]} tab failed to draw: {ex}");
                Controls.Label($"This tab could not be drawn ({ex.GetType().Name}). See the MelonLoader console.");
            }
        }

        private static void DrawStatusLine()
        {
            if (!GameAccess.InGame)
            {
                Controls.Label("Not in a game yet - load a save first.");
                return;
            }
            if (!GameAccess.IsServer)
            {
                Controls.Label("You are a client, not the host. Almost nothing here will work.");
                return;
            }
            Controls.Label("Changing level or day on an existing save can corrupt it. Back the save up first.");
        }

        // ---------------------------------------------------------------- money

        private static void DrawMoneyTab()
        {
            Controls.Header("Money");
            float money = ProgressCheats.Get(ParameterType.Money);
            float wantedMoney = Controls.Stepper("Money", money, MoneySteps);
            if (!Mathf.Approximately(wantedMoney, money))
                ProgressCheats.Set(ParameterType.Money, Mathf.Max(0f, wantedMoney));

            if (Controls.Presets("Set to", new[] { 0f, 10_000f, 100_000f, 1_000_000f, 10_000_000f }, out float preset))
                ProgressCheats.Set(ParameterType.Money, preset);

            Controls.Space();
            bool infinite = Controls.Toggle(ProgressCheats.InfiniteMoney, "Infinite money (tops back up automatically)");
            if (infinite != ProgressCheats.InfiniteMoney)
            {
                ProgressCheats.InfiniteMoney = infinite;
                if (infinite) ProgressCheats.Set(ParameterType.Money, ProgressCheats.InfiniteMoneyTarget);
            }

            _infiniteTarget = Controls.Stepper("Top-up target", ProgressCheats.InfiniteMoneyTarget, MoneySteps);
            if (!Mathf.Approximately(_infiniteTarget, ProgressCheats.InfiniteMoneyTarget))
                ProgressCheats.InfiniteMoneyTarget = Mathf.Max(1f, _infiniteTarget);

            Controls.Space(10f);
            Controls.Header("Crystals");
            StepParameter("Crystals", ParameterType.Crystal, SmallSteps);

            Controls.Space(10f);
            Controls.Header("Shop progression");
            int needed = ProgressCheats.NeedExp();
            Controls.Label(needed < 0
                ? "Experience needed for the next level: the game will not say right now."
                : $"Experience needed for the next level: {needed}");
            StepParameter("Experience", ParameterType.Exp, SmallSteps);
            StepParameter("Level", ParameterType.Level, new[] { 1f, 5f });
            StepParameter("Day", ParameterType.Day, new[] { 1f, 5f });
            StepParameter("Tournament wins", ParameterType.Wins, new[] { 1f, 5f });
        }

        private static void StepParameter(string label, ParameterType type, float[] steps)
        {
            float current = ProgressCheats.Get(type);
            float wanted = Controls.Stepper(label, current, steps);
            if (!Mathf.Approximately(wanted, current))
                ProgressCheats.Set(type, Mathf.Max(0f, wanted));
        }

        // ----------------------------------------------------------------- staff

        private static void DrawStaffTab()
        {
            Controls.Header("Employees");
            Controls.BeginRow();
            if (Controls.Button("Promotion exp to a random employee", 260f))
                EmployeeCheats.GivePromotionExperienceToRandom();
            if (Controls.Button("Spawn cashier", 130f))
                EmployeeCheats.SpawnCashier();
            Controls.EndRow();

            Controls.Space();

            var employees = EmployeeCheats.All();
            if (employees.Count == 0)
            {
                Controls.Label("No employee slots exist in this save yet.");
                return;
            }

            foreach (var info in employees)
            {
                Controls.Label($"#{info.Index}  {info.CurrentType}  -  level {info.Level}, tier {info.TierIndex}, " +
                               $"exp {info.Experience:0.#}{(info.IsWorking ? ", working" : string.Empty)}" +
                               $"{(info.HasDebt ? $", debt {info.DebtAmount}" : string.Empty)}");

                Controls.BeginRow();
                if (Controls.Button("Level -1", 80f)) EmployeeCheats.SetLevel(info, info.Level - 1);
                if (Controls.Button("Level +1", 80f)) EmployeeCheats.SetLevel(info, info.Level + 1);
                if (Controls.Button("Level +10", 90f)) EmployeeCheats.SetLevel(info, info.Level + 10);
                if (Controls.Button("Promote", 80f)) EmployeeCheats.Promote(info);
                if (Controls.Button("Boost", 70f)) EmployeeCheats.Boost(info);
                if (info.HasDebt && Controls.Button("Clear debt", 95f)) EmployeeCheats.ClearDebt(info);
                Controls.EndRow();

                Controls.BeginRow();
                Controls.Label("Tier");
                for (int tier = 0; tier < 4; tier++)
                    if (Controls.Button(tier.ToString(), 36f)) EmployeeCheats.SetTier(info, tier);
                if (Controls.Button("Hire at this tier", 140f)) EmployeeCheats.Hire(info, info.TierIndex);
                Controls.EndRow();
                Controls.Space(4f);
            }
        }

        // ----------------------------------------------------------------- time

        private static void DrawTimeTab()
        {
            Controls.Header("Clock");
            if (TimeCheats.TryGetClock(out int hour, out int minute))
                Controls.Label($"Now: {hour:00}:{minute:00}   (shop hours {TimeCheats.StartHour:00}:00 - {TimeCheats.EndHour:00}:00)");
            else
                Controls.Label("The clock is not running yet.");

            bool running = TimeCheats.IsRunning;
            if (Controls.Button(running ? "Pause time" : "Resume time", 150f))
                TimeCheats.SetRunning(!running);

            Controls.Space();
            Controls.Label($"Jump to: {_wantedHour:00}:{_wantedMinute:00}");
            Controls.BeginRow();
            if (Controls.Button("Hour -1", 80f)) _wantedHour = Wrap(_wantedHour - 1, 0, 23);
            if (Controls.Button("Hour +1", 80f)) _wantedHour = Wrap(_wantedHour + 1, 0, 23);
            if (Controls.Button("Min -15", 80f)) _wantedMinute = Wrap(_wantedMinute - 15, 0, 45);
            if (Controls.Button("Min +15", 80f)) _wantedMinute = Wrap(_wantedMinute + 15, 0, 45);
            if (Controls.Button("Apply", 80f)) TimeCheats.SetClock(_wantedHour, _wantedMinute);
            Controls.EndRow();

            Controls.BeginRow();
            if (Controls.Button("Opening", 90f)) TimeCheats.SetClock(TimeCheats.StartHour, 0);
            if (Controls.Button("Midday", 90f)) TimeCheats.SetClock((TimeCheats.StartHour + TimeCheats.EndHour) / 2, 0);
            if (Controls.Button("Closing", 90f)) TimeCheats.SetClock(TimeCheats.EndHour, 0);
            Controls.EndRow();
            Controls.Label("Times outside shop hours are clamped to the nearest end of the day.");

            Controls.Space(10f);
            Controls.Header("Speed");
            Controls.Label($"Time multiplier: {TimeCheats.Multiplier:0.##}x");
            Controls.BeginRow();
            foreach (float speed in new[] { 0f, 1f, 2f, 5f, 10f, 20f })
                if (Controls.Button(speed <= 0f ? "Freeze" : $"{speed:0}x", 76f))
                    TimeCheats.SetMultiplier(speed);
            Controls.EndRow();

            Controls.Space(10f);
            Controls.Header("Day");
            Controls.BeginRow();
            if (Controls.Button("End the day now", 160f)) TimeCheats.ForceEndDay();
            if (Controls.Button("Skip to next day", 160f)) TimeCheats.NextDay();
            Controls.EndRow();
        }

        private static int Wrap(int value, int min, int max)
        {
            if (value < min) return max;
            if (value > max) return min;
            return value;
        }

        // --------------------------------------------------------------- shelves

        private static void DrawShelvesTab()
        {
            Controls.Header("Shelves");
            Controls.BeginRow();
            if (Controls.Button("Refresh list", 120f))
            {
                _shelves = ShelfCheats.Scan();
                _selectedShelves.Clear();
            }
            Controls.EndRow();
            _aimMode = Controls.Toggle(_aimMode, "Aim mode (act on the shelf you are looking at)");

            if (_aimMode)
            {
                var aimed = ShelfCheats.ShelfUnderCrosshair();
                var described = ShelfCheats.Describe(aimed);
                Controls.Label(described == null
                    ? "Looking at: nothing"
                    : $"Looking at: {described.Label} - {described.ItemCount} item(s) in {described.SlotCount} slot(s)");

                if (aimed != null)
                {
                    Controls.BeginRow();
                    if (Controls.Button("Delete its stock", 160f)) ShelfCheats.Delete(aimed);
                    if (Controls.Button("Return to delivery", 170f)) ShelfCheats.ReturnToDelivery(aimed);
                    if (Controls.Button("Drop one item", 140f)) ShelfCheats.DropRandomItem(aimed);
                    Controls.EndRow();
                }
                Controls.Space();
            }

            if (_shelves == null) _shelves = ShelfCheats.Scan();
            if (_shelves.Count == 0)
            {
                Controls.Label("No sales shelves found.");
                return;
            }

            Controls.BeginRow();
            Controls.Label($"{_selectedShelves.Count} of {_shelves.Count} selected");
            if (Controls.Button("All", 60f))
            {
                _selectedShelves.Clear();
                for (int i = 0; i < _shelves.Count; i++) _selectedShelves.Add(i);
            }
            if (Controls.Button("None", 70f)) _selectedShelves.Clear();
            Controls.EndRow();

            if (_selectedShelves.Count > 0)
            {
                Controls.BeginRow();
                if (Controls.Button("Delete stock on selected", 210f)) ApplyToSelected(false);
                if (Controls.Button("Return selected to delivery", 230f)) ApplyToSelected(true);
                Controls.EndRow();
            }

            Controls.Space();
            for (int i = 0; i < _shelves.Count; i++)
            {
                var entry = _shelves[i];
                bool selected = _selectedShelves.Contains(i);
                bool wanted = Controls.Toggle(selected, $"{entry.Label} - {entry.ItemCount} item(s)");
                if (wanted == selected) continue;
                if (wanted) _selectedShelves.Add(i); else _selectedShelves.Remove(i);
            }
        }

        private static void ApplyToSelected(bool returnToDelivery)
        {
            if (_shelves == null) return;
            int affected = 0;
            foreach (int index in _selectedShelves)
            {
                if (index < 0 || index >= _shelves.Count) continue;
                var shelf = _shelves[index].Shelf;
                affected += returnToDelivery ? ShelfCheats.ReturnToDelivery(shelf) : ShelfCheats.Delete(shelf);
            }
            MelonLogger.Msg($"[CheatForDev] {(returnToDelivery ? "Returned" : "Deleted")} {affected} item(s) " +
                            $"across {_selectedShelves.Count} shelf/shelves.");
            _shelves = ShelfCheats.Scan();
        }

        // ---------------------------------------------------------------- orders

        private const int CatalogPageSize = 18;

        private static void DrawOrdersTab()
        {
            Controls.Header("Free delivery");
            Controls.Label("Boxes are spawned straight onto the delivery point. No basket, no payment.");
            Controls.Label($"Selected: {_selectedBoxLabel}");

            if (_selectedBoxId >= 0)
            {
                Controls.BeginRow();
                foreach (int count in new[] { 1, 5, 10, 25, 50, 100, OrderCheats.MaxPerBurst })
                    if (Controls.Button($"+{count}", 74f))
                        OrderCheats.Spawn(_selectedBoxId, count);
                Controls.EndRow();
            }

            Controls.Space();
            var catalog = OrderCheats.Catalog();
            if (catalog.Count == 0)
            {
                Controls.Label("The product catalog is not loaded yet.");
                return;
            }

            int pages = Mathf.Max(1, Mathf.CeilToInt(catalog.Count / (float)CatalogPageSize));
            _catalogPage = Mathf.Clamp(_catalogPage, 0, pages - 1);

            Controls.BeginRow();
            if (Controls.Button("<< Prev", 90f)) _catalogPage = Mathf.Max(0, _catalogPage - 1);
            Controls.Label($"Page {_catalogPage + 1} of {pages}  ({catalog.Count} boxes)");
            if (Controls.Button("Next >>", 90f)) _catalogPage = Mathf.Min(pages - 1, _catalogPage + 1);
            Controls.EndRow();

            int start = _catalogPage * CatalogPageSize;
            int end = Mathf.Min(catalog.Count, start + CatalogPageSize);
            for (int i = start; i < end; i++)
            {
                var box = catalog[i];
                if (!Controls.Button(box.Label)) continue;
                _selectedBoxId = box.Id;
                _selectedBoxLabel = box.Label;
            }
        }

        // --------------------------------------------------------------- testing

        private static void DrawTestingTab()
        {
            Controls.Header("Customers");
            Controls.Label($"In the shop right now: {TestingCheats.BuyerCount}");
            Controls.BeginRow();
            if (Controls.Button("Customers off", 140f)) TestingCheats.SetBuyersEnabled(false);
            if (Controls.Button("Customers on", 140f)) TestingCheats.SetBuyersEnabled(true);
            if (Controls.Button("Send everyone home", 180f)) TestingCheats.RemoveAllBuyers();
            Controls.EndRow();

            Controls.Space(4f);
            Controls.Label("Spawn one:");
            Controls.BeginRow();
            int drawn = 0;
            foreach (EBuyerType type in Enum.GetValues(typeof(EBuyerType)))
            {
                if (Controls.Button(type.ToString(), 96f)) TestingCheats.SpawnBuyer(type);
                if (++drawn % 4 != 0) continue;
                Controls.EndRow();
                Controls.BeginRow();
            }
            Controls.EndRow();

            Controls.Space(10f);
            Controls.Header("Shop state");
            Controls.Label($"Cleanliness: {TestingCheats.Cleanliness:0.00}");
            if (Controls.Button("Clean everything", 160f)) TestingCheats.ClearAllDirt();

            Controls.Space(10f);
            Controls.Header("Move around");
            if (Controls.Button("Teleport to the delivery point", 240f)) TestingCheats.TeleportToDelivery();
            Controls.Label("Tip: customers off plus 20x time is the fastest way to watch employee AI for a whole day.");
        }

        // -------------------------------------------------------------- licences

        private static List<LicenseCheats.LicenseEntry> _licenses;
        private static bool _showLicenseList;

        private static void DrawLicenses()
        {
            Controls.Header("Licences");
            if (_licenses == null) _licenses = LicenseCheats.All();

            if (_licenses.Count == 0)
            {
                Controls.Label("No licences found in this save yet.");
                return;
            }

            Controls.Label($"Owned: {LicenseCheats.OwnedCount(_licenses)} of {_licenses.Count}");
            Controls.BeginRow();
            if (Controls.Button("Unlock every licence", 200f))
            {
                LicenseCheats.UnlockAll();
                _licenses = LicenseCheats.All();
            }
            if (Controls.Button("Refresh", 100f)) _licenses = LicenseCheats.All();
            Controls.EndRow();

            _showLicenseList = Controls.Toggle(_showLicenseList, "Show them one by one");
            if (!_showLicenseList) return;

            foreach (var license in _licenses)
            {
                if (license.Owned)
                {
                    Controls.Label($"  {license.Label} - owned");
                    continue;
                }
                if (!Controls.Button($"Unlock {license.Label}")) continue;
                LicenseCheats.Unlock(license.Id);
                _licenses = LicenseCheats.All();
            }
        }

        // --------------------------------------------------------------- unlocks

        private static void DrawUnlocksTab()
        {
            Controls.Header("Upgrades");
            Controls.Label($"Store level: {UnlockCheats.StoreLevel}");
            Controls.BeginRow();
            if (Controls.Button("Buy next store upgrade", 210f)) UnlockCheats.BuyNextUpgrade(UpgradeType.Store);
            if (Controls.Button("Buy next storage upgrade", 220f)) UnlockCheats.BuyNextUpgrade(UpgradeType.Storage);
            Controls.EndRow();

            Controls.Space(10f);
            DrawLicenses();

            Controls.Space(10f);
            Controls.Header("Bills");
            Controls.Label($"Outstanding bills and rent: {UnlockCheats.OutstandingBills:0}");
            Controls.Label("Pay them from the in-game computer; this menu only reports the amount.");

            Controls.Space(10f);
            Controls.Header("The game's own cheat panel");
            if (!UnlockCheats.HasNative)
            {
                Controls.Label("Not present in this build, so the actions below are unavailable.");
                return;
            }

            Controls.Label("Found it in the scene - these call the developers' own code.");
            Controls.BeginRow();
            if (Controls.Button("Open all cards", 160f)) UnlockCheats.OpenAllCards();
            if (Controls.Button("Unlock inventory tools", 200f)) UnlockCheats.UnlockInventoryTools();
            if (Controls.Button("Spawn buildings", 160f)) UnlockCheats.SpawnBuildings();
            Controls.EndRow();
            Controls.BeginRow();
            if (Controls.Button("Toggle the game's panel", 210f)) UnlockCheats.ToggleNativePanel();
            Controls.EndRow();
        }
    }
}

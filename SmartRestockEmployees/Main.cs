using AnimeShopMods;
using AnimeShopMods.Ui;
using MelonLoader;
using SmartRestockEmployees.Ui;
using UnityEngine;

[assembly: MelonInfo(typeof(SmartRestockEmployees.Main), SmartRestockEmployees.ModInfo.Name, SmartRestockEmployees.ModInfo.Version, SmartRestockEmployees.ModInfo.Author, SmartRestockEmployees.ModInfo.DownloadLink)]
//[assembly: MelonGame("OneMoreTime", "Anime Shop Simulator")]
[assembly: MelonColor(255, 255, 140, 200)]
[assembly: MelonAuthorColor(255, 120, 200, 255)]

namespace SmartRestockEmployees
{
    // Single source of truth for mod metadata, shared by MelonInfo and AssemblyInfo.
    public static class ModInfo
    {
        public const string Name = "Smart Restock Employees";
        public const string Version = "1.4.0";
        public const string Author = "1REDfriend";
        // Fill in the Nexus Mods page URL after the first upload, then rebuild.
        public const string DownloadLink = null;
        public const string Description =
            "Anime Shop Simulator mod: employees restock the emptiest shelf first and store boxes on the least-filled storage rack.";
    }

    public class Main : MelonMod
    {
        private static MelonPreferences_Entry<bool> _emptyShelvesFirst;
        private static MelonPreferences_Entry<bool> _keepEmptySlotProduct;
        private static MelonPreferences_Entry<bool> _fillFreeSlots;
        private static MelonPreferences_Entry<bool> _stuckWatchdog;
        private static MelonPreferences_Entry<float> _stuckSeconds;
        private static MelonPreferences_Entry<bool> _verboseLogs;
        private static MelonPreferences_Entry<bool> _diagnosticLogs;
        private static MelonPreferences_Entry<string> _panelKey;

        public static KeyCode PanelKey { get; private set; } = KeyCode.F7;
        public static bool PanelVisible { get; private set; }

        public static bool EmptyShelvesFirst => _emptyShelvesFirst == null || _emptyShelvesFirst.Value;
        public static bool KeepEmptySlotProduct => _keepEmptySlotProduct == null || _keepEmptySlotProduct.Value;
        public static bool FillFreeSlots => _fillFreeSlots == null || _fillFreeSlots.Value;
        public static bool StuckWatchdogEnabled => _stuckWatchdog == null || _stuckWatchdog.Value;
        public static float StuckSeconds => _stuckSeconds == null ? 15f : _stuckSeconds.Value;
        public static bool VerboseLogs => _verboseLogs != null && _verboseLogs.Value;
        public static bool DiagnosticLogs => _diagnosticLogs != null && _diagnosticLogs.Value;

        public override void OnInitializeMelon()
        {
            var category = MelonPreferences.CreateCategory("SmartRestockEmployees", ModInfo.Name);
            _emptyShelvesFirst = category.CreateEntry("EmptyShelvesFirst", true, "Empty shelves first",
                "Fill shelf slots that are completely empty before topping up slots that still have stock.");
            _keepEmptySlotProduct = category.CreateEntry("KeepEmptySlotProduct", true, "Keep empty slot product",
                "An emptied shelf slot is only refilled with the product it held before.");
            _fillFreeSlots = category.CreateEntry("FillFreeSlots", true, "Fill free slots",
                "Let employees put stock in slots that have never held anything. " +
                "Turn this off to keep bare shelves bare.");
            _stuckWatchdog = category.CreateEntry("StuckWatchdog", true, "Stuck watchdog",
                "Recover employees that stand still while walking to a shelf or disposing a box.");
            _stuckSeconds = category.CreateEntry("StuckSeconds", 15f, "Stuck seconds",
                "How long an employee may stand still before the watchdog steps in.");
            _verboseLogs = category.CreateEntry("VerboseLogs", false, "Verbose logs",
                "Log every retarget decision to the MelonLoader console.");
            _diagnosticLogs = category.CreateEntry("DiagnosticLogs", false, "Diagnostic logs",
                "Trace the game's own restock search step by step. Very noisy; for bug hunting only.");
            // F7, not F8: CheatForDev owns F8 and some players run both.
            _panelKey = category.CreateEntry("PanelKey", "F7", "Panel key",
                "Key that opens the shelf panel. Any UnityEngine.KeyCode name.");

            if (!string.IsNullOrEmpty(_panelKey.Value) &&
                System.Enum.TryParse<KeyCode>(_panelKey.Value, true, out var parsed))
                PanelKey = parsed;

            MelonLogger.Msg($"{ModInfo.Name} {ModInfo.Version} loaded. Press {PanelKey} for the shelf panel.");
        }

        public override void OnUpdate()
        {
            if (StuckWatchdogEnabled)
                StuckWatchdog.Tick();

            if (Input.GetKeyDown(PanelKey))
                SetPanelVisible(!PanelVisible);

            if (!PanelVisible) return;

            if (!GameLinks.InGame) SetPanelVisible(false);
            else CursorControl.Tick();
        }

        private static void SetPanelVisible(bool visible)
        {
            PanelVisible = visible;
            if (visible) CursorControl.Acquire(GameLinks.Ui);
            else CursorControl.Release();
            ShelfPanel.Reset();
        }

        // The frame the capability survey owns. Nothing else is drawn during it.
        private static int _surveyFrame = -1;

        public override void OnGUI()
        {
            if (!PanelVisible) return;

            // The survey owns a whole frame, not merely its first pass. IMGUI matches controls between
            // the layout and repaint passes and throws if they differ, and GuiCaps.Surveyed flips on
            // the first pass -- gating on that alone draws the survey at Layout and the panel at
            // Repaint, which is exactly the mismatch it is meant to avoid.
            if (_surveyFrame < 0) _surveyFrame = Time.frameCount;
            if (Time.frameCount == _surveyFrame)
            {
                GuiCaps.Survey();
                return;
            }

            Skin.EnsureBuilt();
            ShelfPanel.Draw();
        }

        public override void OnSceneWasUnloaded(int buildIndex, string sceneName)
        {
            StuckWatchdog.Reset();
            Diagnostics.Reset();
            SearchContext.Reset();
            TargetBlacklist.Reset();
            TargetClaims.Reset();

            GameLinks.Reset();
            GuiCaps.Reset();
            _surveyFrame = -1;
            Skin.Reset();
            CursorControl.Reset();
            SetPanelVisible(false);
        }

        public static void SetFillFreeSlots(bool value)
        {
            if (_fillFreeSlots == null) return;
            _fillFreeSlots.Value = value;
            MelonPreferences.Save();
        }

        public static void Log(string message)
        {
            if (VerboseLogs)
                MelonLogger.Msg(message);
        }
    }
}

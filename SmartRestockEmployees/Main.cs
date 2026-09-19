using MelonLoader;

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
        public const string Version = "1.3.0";
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

            MelonLogger.Msg($"{ModInfo.Name} {ModInfo.Version} loaded.");
        }

        public override void OnUpdate()
        {
            if (StuckWatchdogEnabled)
                StuckWatchdog.Tick();
        }

        public override void OnSceneWasUnloaded(int buildIndex, string sceneName)
        {
            StuckWatchdog.Reset();
            Diagnostics.Reset();
            SearchContext.Reset();
            TargetBlacklist.Reset();
            TargetClaims.Reset();
        }

        public static void Log(string message)
        {
            if (VerboseLogs)
                MelonLogger.Msg(message);
        }
    }
}

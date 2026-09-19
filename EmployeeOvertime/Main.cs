using HarmonyLib;
using Il2CppProject.Code.Gameplay.Controllers;
using MelonLoader;

[assembly: MelonInfo(typeof(EmployeeOvertime.Main), EmployeeOvertime.ModInfo.Name, EmployeeOvertime.ModInfo.Version, EmployeeOvertime.ModInfo.Author, EmployeeOvertime.ModInfo.DownloadLink)]
//[assembly: MelonGame("OneMoreTime", "Anime Shop Simulator")]
//[assembly: MelonGame("OneMoreTime", "Anime Shop Simulator ")]
[assembly: MelonColor(255, 255, 190, 90)]
[assembly: MelonAuthorColor(255, 120, 200, 255)]

namespace EmployeeOvertime
{
    // Single source of truth for mod metadata, shared by MelonInfo and AssemblyInfo.
    public static class ModInfo
    {
        public const string Name = "Employee Overtime";
        public const string Version = "0.1.1";
        public const string Author = "1REDfriend";
        // Fill in the Nexus Mods page URL after the first upload, then rebuild.
        public const string DownloadLink = null;
        public const string Description =
            "Anime Shop Simulator mod: employees keep working after the store closes, restocking and cleaning until you end the day.";
    }

    public class Main : MelonMod
    {
        private static MelonPreferences_Category _category;
        private static MelonPreferences_Entry<bool> _enabled;
        private static MelonPreferences_Entry<bool> _verboseLogs;

        public static bool Enabled => _enabled == null || _enabled.Value;
        public static bool VerboseLogs => _verboseLogs != null && _verboseLogs.Value;

        public override void OnInitializeMelon()
        {
            _category = MelonPreferences.CreateCategory("EmployeeOvertime", ModInfo.Name);
            _enabled = _category.CreateEntry("Enabled", true, "Enabled",
                "Keep employees working after the store closes.");
            _verboseLogs = _category.CreateEntry("VerboseLogs", true, "Verbose logs",
                "Log day-end and day-start events to the MelonLoader console.");

            MelonLogger.Msg($"{ModInfo.Name} {ModInfo.Version} loaded. Enabled: {Enabled}");
        }

        public static void Log(string message)
        {
            if (VerboseLogs)
                MelonLogger.Msg(message);
        }
    }

    // True between closing time and the start of the next day while overtime is active.
    internal static class OvertimeState
    {
        public static bool Active;
    }

    // At closing time the game marks the day as ended and refreshes every employee's work state,
    // which sends them idle. After the original runs we mark the day as still started and refresh
    // again, so employees resume their work controllers until the next day begins.
    [HarmonyPatch(typeof(EmployeesController), "HandleDayEnd")]
    public static class HandleDayEndPatch
    {
        [HarmonyPrefix]
        public static void Prefix(EmployeesController __instance)
        {
            if (!Main.Enabled) return;
            Main.Log($"[Overtime] Day end: isDayStarted={__instance._isDayStarted}, employees={CountEmployees(__instance)}");
        }

        [HarmonyPostfix]
        public static void Postfix(EmployeesController __instance)
        {
            if (!Main.Enabled) return;

            try
            {
                __instance._isDayStarted = true;
                __instance.RefreshAllWorkStates();
                OvertimeState.Active = true;
                Main.Log($"[Overtime] Overtime started: employees={CountEmployees(__instance)}");
            }
            catch (System.Exception ex)
            {
                OvertimeState.Active = false;
                MelonLogger.Error($"[Overtime] Failed to start overtime, employees follow normal hours: {ex}");
            }
        }

        internal static int CountEmployees(EmployeesController controller)
        {
            var employees = controller._employees;
            return employees == null ? -1 : employees.Count;
        }
    }

    // Restore the flag the game expects before its own day-start logic runs, so the next day starts
    // exactly as it would without the mod.
    [HarmonyPatch(typeof(EmployeesController), "HandleNextDayStart")]
    public static class HandleNextDayStartPatch
    {
        [HarmonyPrefix]
        public static void Prefix(EmployeesController __instance)
        {
            EndOvertime(__instance, "next day start");
        }

        internal static void EndOvertime(EmployeesController controller, string reason)
        {
            if (!OvertimeState.Active) return;

            OvertimeState.Active = false;
            controller._isDayStarted = false;
            Main.Log($"[Overtime] Overtime ended ({reason}): employees={HandleDayEndPatch.CountEmployees(controller)}");
        }
    }

    [HarmonyPatch(typeof(EmployeesController), "HandleDayStart")]
    public static class HandleDayStartPatch
    {
        [HarmonyPrefix]
        public static void Prefix(EmployeesController __instance)
        {
            HandleNextDayStartPatch.EndOvertime(__instance, "day start");
        }

        [HarmonyPostfix]
        public static void Postfix(EmployeesController __instance)
        {
            if (!Main.Enabled) return;
            Main.Log($"[Overtime] Day start: isDayStarted={__instance._isDayStarted}, employees={HandleDayEndPatch.CountEmployees(__instance)}");
        }
    }
}

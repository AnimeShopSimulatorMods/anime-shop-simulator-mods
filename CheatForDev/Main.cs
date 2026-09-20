using AnimeShopMods;
using AnimeShopMods.Ui;
using CheatForDev.Cheats;
using CheatForDev.Ui;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(CheatForDev.Main), CheatForDev.ModInfo.Name, CheatForDev.ModInfo.Version, CheatForDev.ModInfo.Author, CheatForDev.ModInfo.DownloadLink)]
//[assembly: MelonGame("OneMoreTime", "Anime Shop Simulator")]
[assembly: MelonColor(255, 255, 120, 120)]
[assembly: MelonAuthorColor(255, 120, 200, 255)]

namespace CheatForDev
{
    // Single source of truth for mod metadata, shared by MelonInfo and AssemblyInfo.
    public static class ModInfo
    {
        public const string Name = "Cheat for Dev";
        public const string Version = "0.5.0";
        public const string Author = "1REDfriend";
        public const string DownloadLink = null;
        public const string Description =
            "Anime Shop Simulator developer cheat menu: money, progression, time, shelves, orders and test helpers.";
    }

    public class Main : MelonMod
    {
        private static MelonPreferences_Entry<string> _toggleKey;
        private static MelonPreferences_Entry<bool> _runProbe;
        private static MelonPreferences_Entry<bool> _verboseLogs;
        private static MelonPreferences_Entry<float> _windowX;
        private static MelonPreferences_Entry<float> _windowY;

        public static KeyCode ToggleKey { get; private set; } = KeyCode.F8;
        public static bool VerboseLogs => _verboseLogs != null && _verboseLogs.Value;
        public static bool RunProbe => _runProbe != null && _runProbe.Value;
        public static bool MenuVisible { get; private set; }

        public override void OnInitializeMelon()
        {
            var category = MelonPreferences.CreateCategory("CheatForDev", ModInfo.Name);
            _toggleKey = category.CreateEntry("ToggleKey", "F8", "Toggle key",
                "Key that opens and closes the cheat menu. Any UnityEngine.KeyCode name.");
            _runProbe = category.CreateEntry("RunProbe", true, "Run probe",
                "Log a survey of the game's cheat-related objects once per session. Useful after a game update.");
            _verboseLogs = category.CreateEntry("VerboseLogs", false, "Verbose logs",
                "Log every cheat action to the MelonLoader console.");

            _windowX = category.CreateEntry("WindowX", 24f, "Window X",
                "Where the menu sits on screen. Updated whenever the window is dragged.");
            _windowY = category.CreateEntry("WindowY", 24f, "Window Y",
                "Where the menu sits on screen. Updated whenever the window is dragged.");

            ToggleKey = ParseKey(_toggleKey.Value);
            CheatWindow.MoveTo(_windowX.Value, _windowY.Value);
            MelonLogger.Msg($"{ModInfo.Name} {ModInfo.Version} loaded. Press {ToggleKey} to open the menu.");
            MelonLogger.Msg($"  game: {Application.productName} {Application.version}, unity {Application.unityVersion}");

            // Almost every bug report about this mod comes down to "I pressed it and nothing happened",
            // which the verbose log answers in one line. Say where the switch is while it is off.
            if (!VerboseLogs)
                MelonLogger.Msg("  verbose logs are off. Turn on VerboseLogs in UserData/MelonPreferences.cfg " +
                                "under [CheatForDev] before reporting anything that looks like it does nothing.");
        }

        public override void OnUpdate()
        {
            if (RunProbe) Probe.Tick();

            if (Input.GetKeyDown(ToggleKey))
                SetMenuVisible(!MenuVisible);

            if (MenuVisible)
            {
                if (!GameAccess.InGame) SetMenuVisible(false);
                else CursorControl.Tick();
            }

            ProgressCheats.Tick();
        }

        public override void OnGUI()
        {
            if (MenuVisible)
                CheatWindow.Draw();
        }

        public override void OnSceneWasUnloaded(int buildIndex, string sceneName)
        {
            SetMenuVisible(false);
            Probe.Reset();
            GameAccess.Reset();
            UnlockCheats.Reset();
            TutorialCheats.Reset();
            CheatWindow.Reset();
            GuiCaps.Reset();
            CursorControl.Reset();
        }

        // Called when a drag finishes, so the menu opens where it was left next time.
        public static void SaveWindowPosition(float x, float y)
        {
            if (_windowX == null || _windowY == null) return;
            if (Mathf.Approximately(_windowX.Value, x) && Mathf.Approximately(_windowY.Value, y)) return;

            _windowX.Value = x;
            _windowY.Value = y;
            MelonPreferences.Save();
        }

        public static void Log(string message)
        {
            if (VerboseLogs)
                MelonLogger.Msg(message);
        }

        private static void SetMenuVisible(bool visible)
        {
            if (visible == MenuVisible) return;

            if (visible && !GameAccess.InGame)
            {
                MelonLogger.Msg("[CheatForDev] Load a save first; the menu needs a running game.");
                return;
            }

            MenuVisible = visible;
            if (visible) CursorControl.Acquire(GameAccess.Ui);
            else CursorControl.Release();
        }

        private static KeyCode ParseKey(string value)
        {
            if (!string.IsNullOrEmpty(value) && System.Enum.TryParse<KeyCode>(value, true, out var parsed))
                return parsed;
            MelonLogger.Warning($"[CheatForDev] Unknown toggle key '{value}', falling back to F8.");
            return KeyCode.F8;
        }
    }
}

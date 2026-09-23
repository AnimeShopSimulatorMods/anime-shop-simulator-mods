using GameBridge.Net;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(GameBridge.Main), GameBridge.ModInfo.Name, GameBridge.ModInfo.Version, GameBridge.ModInfo.Author, GameBridge.ModInfo.DownloadLink)]
[assembly: MelonColor(255, 160, 160, 160)]
[assembly: MelonAuthorColor(255, 120, 200, 255)]

namespace GameBridge
{
    public static class ModInfo
    {
        public const string Name = "Game Bridge";
        public const string Version = "0.1.0";
        public const string Author = "1REDfriend";
        public const string DownloadLink = null;
        public const string Description =
            "Dev-only: lets the Game MCP server inspect and drive the running game. Never ship to players.";
    }

    public class Main : MelonMod
    {
        public const int DefaultPort = 47831;
        private const int CommandsPerFrame = 8;

        private static MelonPreferences_Entry<int> _port;
        private static MelonPreferences_Entry<bool> _verboseLogs;

        public static Dispatcher Dispatcher { get; } = new Dispatcher();
        private LineServer _server;

        public override void OnInitializeMelon()
        {
            var category = MelonPreferences.CreateCategory("GameBridge", ModInfo.Name);
            _port = category.CreateEntry("Port", DefaultPort, "Port",
                "Loopback port the Game MCP server connects to. Must match GAME_BRIDGE_PORT in .mcp.json.");
            _verboseLogs = category.CreateEntry("VerboseLogs", false, "Verbose logs",
                "Log every cheat action the bridge runs.");

            AnimeShopMods.Dev.DevLog.VerboseSwitch = () => _verboseLogs.Value;
            Dispatcher.LogError = MelonLogger.Error;

            // Claude drives the game while the user is elsewhere; a paused player would time out every command.
            Application.runInBackground = true;

            Commands.Registry.RegisterAll(Dispatcher);

            _server = new LineServer(Dispatcher, _port.Value) { LogError = MelonLogger.Error };
            try
            {
                _server.Start();
                MelonLogger.Msg($"{ModInfo.Name} {ModInfo.Version} listening on 127.0.0.1:{_server.Port}.");
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"[GameBridge] Could not listen on port {_port.Value}: {ex.Message}");
            }
        }

        public override void OnUpdate() => Dispatcher.Pump(CommandsPerFrame);

        public override void OnSceneWasUnloaded(int buildIndex, string sceneName)
        {
            // A held key must not survive into whatever scene loads next.
            Input.Win32Input.ReleaseAll();
            Reflection.Handles.Clear(sceneName);
            AnimeShopMods.Dev.GameAccess.Reset();
        }

        public override void OnApplicationQuit()
        {
            Input.Win32Input.ReleaseAll();
            _server?.Stop();
        }
    }
}

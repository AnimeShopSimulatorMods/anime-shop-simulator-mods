using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace GameMcp.Tools;

[McpServerToolType]
public static class BasicTools
{
    [McpServerTool(Name = "ping"), Description("Check that the game is running with GameBridge loaded, and list the bridge's commands.")]
    public static Task<CallToolResult> Ping(BridgeClient bridge, CancellationToken cancel) =>
        Relay.Run(bridge, "ping", null, cancel: cancel);

    [McpServerTool(Name = "read_log"), Description("Read MelonLoader's log from disk. Works with the game closed. " +
                                                   "Filter with a case-insensitive regex, then keep the last N lines.")]
    public static CallToolResult ReadLog(GamePaths paths,
        [Description("How many lines to return, from the end.")] int lines = 100,
        [Description("Optional regex, e.g. 'ShelfLocks|SmartSorting|Exception'.")] string grep = null,
        [Description("Read Latest.log.prev (the run before this one).")] bool previous = false)
    {
        try
        {
            return Relay.Text(LogReader.Read(paths.Log(previous), lines, grep));
        }
        catch (ArgumentException ex)
        {
            // A malformed grep pattern is Claude's typo, not a server failure; say so as a tool error.
            return Relay.Error($"Bad grep pattern '{grep}': {ex.Message}");
        }
    }
}

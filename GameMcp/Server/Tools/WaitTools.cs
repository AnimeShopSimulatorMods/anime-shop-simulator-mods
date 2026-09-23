using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace GameMcp.Tools;

[McpServerToolType]
public static class WaitTools
{
    [McpServerTool(Name = "wait"), Description("Let the game run, then return. Either real seconds (max 600) or " +
        "game-clock minutes (stops when that many in-game minutes pass, or after 600 real seconds).")]
    public static Task<CallToolResult> Wait(BridgeClient bridge, float seconds = 0, int gameMinutes = 0,
        CancellationToken cancel = default)
    {
        int budgetMs = (int)(((seconds > 0 ? seconds : 600) + 10) * 1000);
        return Relay.Run(bridge, "wait", new { seconds, gameMinutes }, budgetMs, cancel);
    }
}

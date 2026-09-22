using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace GameMcp.Tools;

// Everything here CHANGES THE SAVE. Use a test save.
[McpServerToolType]
public static class CheatTools
{
    [McpServerTool(Name = "progress"), Description("CHANGES THE SAVE. No parameter: list every progress value " +
        "(Money, Day, Exp, Level, Crystal, Wins...). With parameter: set or add to it.")]
    public static Task<CallToolResult> Progress(BridgeClient bridge, string parameter = null, float? set = null,
        float? add = null, CancellationToken cancel = default) =>
        Relay.Run(bridge, "progress", new { parameter, set, add }, cancel: cancel);

    [McpServerTool(Name = "box_catalog"), Description("Every box type that can be spawned, with its id.")]
    public static Task<CallToolResult> BoxCatalog(BridgeClient bridge, CancellationToken cancel = default) =>
        Relay.Run(bridge, "box_catalog", null, cancel: cancel);

    [McpServerTool(Name = "spawn_boxes"), Description("CHANGES THE SAVE. Deliver boxes of a catalog id for free.")]
    public static Task<CallToolResult> SpawnBoxes(BridgeClient bridge, int id, int count = 1, CancellationToken cancel = default) =>
        Relay.Run(bridge, "spawn_boxes", new { id, count }, cancel: cancel);

    [McpServerTool(Name = "clear_boxes"), Description("CHANGES THE SAVE. Remove loose boxes: all, or one catalog id.")]
    public static Task<CallToolResult> ClearBoxes(BridgeClient bridge, int? id = null, CancellationToken cancel = default) =>
        Relay.Run(bridge, "clear_boxes", new { id }, cancel: cancel);

    [McpServerTool(Name = "time"), Description("CHANGES THE SAVE. Game clock: set hour/minute, pause (running=false), " +
        "clock multiplier, end the day, or skip to the next day. No args: just read it.")]
    public static Task<CallToolResult> GameTime(BridgeClient bridge, int? hour = null, int? minute = null, bool? running = null,
        float? multiplier = null, bool endDay = false, bool nextDay = false, CancellationToken cancel = default) =>
        Relay.Run(bridge, "time", new { hour, minute, running, multiplier, endDay, nextDay }, cancel: cancel);

    [McpServerTool(Name = "time_scale"), Description("Unity's global speed (0-20). 1 is normal. Speeds up employees, " +
        "buyers and the clock together. Not saved. No args: read it.")]
    public static Task<CallToolResult> TimeScale(BridgeClient bridge, float? scale = null, CancellationToken cancel = default) =>
        Relay.Run(bridge, "time_scale", new { scale }, cancel: cancel);

    [McpServerTool(Name = "employee"), Description("CHANGES THE SAVE. action: level|experience|tier|promote|boost|" +
        "clear_debt|hire|profession (need index from 'employees' roster; value where it applies) or spawn_cashier|promotion_xp.")]
    public static Task<CallToolResult> Employee(BridgeClient bridge, string action, int index = 0, string value = null,
        CancellationToken cancel = default) =>
        Relay.Run(bridge, "employee", new { action, index, value = ParseValue(value) }, cancel: cancel);

    [McpServerTool(Name = "empty_shelf"), Description("CHANGES THE SAVE. Empty a shelf (handle from 'shelves'): " +
        "mode 'delivery' packs items into boxes at the delivery zone, 'delete' destroys them.")]
    public static Task<CallToolResult> EmptyShelf(BridgeClient bridge, string shelf, string mode = "delivery", CancellationToken cancel = default) =>
        Relay.Run(bridge, "empty_shelf", new { shelf, mode }, cancel: cancel);

    [McpServerTool(Name = "buyers"), Description("Customers: enable/disable spawning, remove all, spawn one by type, " +
        "or clean all dirt. No args: read state and list buyer types.")]
    public static Task<CallToolResult> Buyers(BridgeClient bridge, bool? enabled = null, bool removeAll = false,
        string spawn = null, bool clean = false, CancellationToken cancel = default) =>
        Relay.Run(bridge, "buyers", new { enabled, removeAll, spawn, clean }, cancel: cancel);

    // Numbers go to the bridge as numbers, so "3" and 3 behave the same; anything else (enum names) stays a string.
    private static object ParseValue(string value) =>
        value == null ? null : double.TryParse(value, out var number) ? number : value;
}

using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace GameMcp.Tools;

[McpServerToolType]
public static class StateTools
{
    [McpServerTool(Name = "game_status"), Description("Scene, whether a store is loaded, host, clock, day, money, " +
        "time scale, window focus, player position, and loaded mods.")]
    public static Task<CallToolResult> GameStatus(BridgeClient bridge, CancellationToken cancel = default) =>
        Relay.Run(bridge, "game_status", null, cancel: cancel);

    [McpServerTool(Name = "shelves"), Description("Every shelf and slot: persistent id, product, LastDefinitionId, " +
        "whether the slot is bound to a product, count/max, HavePoints, game lock, Smart Restock lock, handles.")]
    public static Task<CallToolResult> Shelves(BridgeClient bridge,
        [Description("Only slots holding nothing.")] bool onlyEmpty = false, CancellationToken cancel = default) =>
        Relay.Run(bridge, "shelves", new { onlyEmpty }, cancel: cancel);

    [McpServerTool(Name = "employees"), Description("Employee roster (level, tier, role...) and every sorting " +
        "employee's position, carried product and target slot.")]
    public static Task<CallToolResult> Employees(BridgeClient bridge, CancellationToken cancel = default) =>
        Relay.Run(bridge, "employees", null, cancel: cancel);

    [McpServerTool(Name = "boxes"), Description("Product boxes in the world, grouped by product.")]
    public static Task<CallToolResult> Boxes(BridgeClient bridge, bool positions = false, CancellationToken cancel = default) =>
        Relay.Run(bridge, "boxes", new { positions }, cancel: cancel);
}

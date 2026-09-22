using System.ComponentModel;
using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace GameMcp.Tools;

[McpServerToolType]
public static class ReflectionTools
{
    [McpServerTool(Name = "find_objects"), Description("Find live game objects by type name (full or short, e.g. " +
        "'ProductPricePlace'). Returns handles like 'h:12' for get/set/call. Handles die on scene change.")]
    public static Task<CallToolResult> FindObjects(BridgeClient bridge, string type, int limit = 50, CancellationToken cancel = default) =>
        Relay.Run(bridge, "find_objects", new { type, limit }, cancel: cancel);

    [McpServerTool(Name = "get"), Description("Read a member path on a handle ('h:12') or a static type ('type:Full.Name'). " +
        "Path is dotted, e.g. '_productPlace.Count'. Private members work.")]
    public static Task<CallToolResult> Get(BridgeClient bridge, string target, string path, CancellationToken cancel = default) =>
        Relay.Run(bridge, "get", new { target, path }, cancel: cancel);

    [McpServerTool(Name = "set"), Description("CHANGES GAME STATE (may end up in the save). Write a member path on a " +
        "handle or static type. Value is JSON: number, string, enum name, {x,y,z}, or a handle.")]
    public static Task<CallToolResult> Set(BridgeClient bridge, string target, string path, JsonNode value, CancellationToken cancel = default) =>
        Relay.Run(bridge, "set", new { target, path, value }, cancel: cancel);

    [McpServerTool(Name = "call"), Description("MAY CHANGE GAME STATE. Call a method on a handle or static type. " +
        "Args are a JSON array of numbers, strings, enum names, {x,y,z} or handles.")]
    public static Task<CallToolResult> Call(BridgeClient bridge, string target, string method, JsonArray args = null, CancellationToken cancel = default) =>
        Relay.Run(bridge, "call", new { target, method, args }, cancel: cancel);

    [McpServerTool(Name = "mod_call"), Description("MAY CHANGE GAME STATE. Call a static method of any loaded mod or game " +
        "type, internal ones included, e.g. type 'SmartRestockEmployees.ShelfLocks', method 'LockEmptySlots'.")]
    public static Task<CallToolResult> ModCall(BridgeClient bridge, string type, string method, JsonArray args = null, CancellationToken cancel = default) =>
        Relay.Run(bridge, "mod_call", new { type, method, args }, cancel: cancel);

    [McpServerTool(Name = "mod_prefs"), Description("Read MelonPreferences. No category: list categories. Category only: " +
        "list its values. Category + entry + value: set it and save the cfg.")]
    public static Task<CallToolResult> ModPrefs(BridgeClient bridge, string category = null, string entry = null,
        JsonNode value = null, CancellationToken cancel = default) =>
        Relay.Run(bridge, "mod_prefs", new { category, entry, value }, cancel: cancel);
}

using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace GameMcp.Tools;

// Real OS input: it goes to the game window, which the bridge brings to the front first. Anything the
// user is typing elsewhere at the same moment can be interrupted.
[McpServerToolType]
public static class InputTools
{
    [McpServerTool(Name = "key_press"), Description("Press and release a key, holding it for holdSeconds (max 30). " +
        "Keys: letters, digits, F1-F12, Space, Enter, Escape, Tab, Shift, Ctrl, Alt, Up/Down/Left/Right...")]
    public static Task<CallToolResult> KeyPress(BridgeClient bridge, string key, float holdSeconds = 0.1f,
        CancellationToken cancel = default) =>
        Relay.Run(bridge, "key_press", new { key, holdSeconds }, (int)((holdSeconds + 10) * 1000), cancel);

    [McpServerTool(Name = "key_down"), Description("Hold a key down until key_up or key_release_all.")]
    public static Task<CallToolResult> KeyDown(BridgeClient bridge, string key, CancellationToken cancel = default) =>
        Relay.Run(bridge, "key_down", new { key }, cancel: cancel);

    [McpServerTool(Name = "key_up"), Description("Release a held key.")]
    public static Task<CallToolResult> KeyUp(BridgeClient bridge, string key, CancellationToken cancel = default) =>
        Relay.Run(bridge, "key_up", new { key }, cancel: cancel);

    [McpServerTool(Name = "key_release_all"), Description("Release every key the bridge is holding.")]
    public static Task<CallToolResult> KeyReleaseAll(BridgeClient bridge, CancellationToken cancel = default) =>
        Relay.Run(bridge, "key_release_all", null, cancel: cancel);

    [McpServerTool(Name = "type_text"), Description("Type text into whatever field has focus in the game.")]
    public static Task<CallToolResult> TypeText(BridgeClient bridge, string text, CancellationToken cancel = default) =>
        Relay.Run(bridge, "type_text", new { text }, cancel: cancel);

    [McpServerTool(Name = "mouse_move"), Description("Move the mouse by dx, dy pixels (turns the camera in first person).")]
    public static Task<CallToolResult> MouseMove(BridgeClient bridge, int dx, int dy, CancellationToken cancel = default) =>
        Relay.Run(bridge, "mouse_move", new { dx, dy }, cancel: cancel);

    [McpServerTool(Name = "mouse_click"), Description("Click the left or right mouse button.")]
    public static Task<CallToolResult> MouseClick(BridgeClient bridge, string button = "left", CancellationToken cancel = default) =>
        Relay.Run(bridge, "mouse_click", new { button }, cancel: cancel);
}

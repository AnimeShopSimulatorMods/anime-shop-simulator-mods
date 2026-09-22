using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace GameMcp.Tools;

[McpServerToolType]
public static class VisualTools
{
    [McpServerTool(Name = "screenshot"), Description("Capture what the game shows right now, as an image.")]
    public static async Task<CallToolResult> Screenshot(BridgeClient bridge, CancellationToken cancel = default)
    {
        BridgeReply reply;
        try
        {
            reply = await bridge.CallAsync("screenshot", new { superSize = 1 }, 10000, cancel);
        }
        catch (BridgeUnavailableException ex)
        {
            return Relay.Error(ex.Message);
        }
        if (!reply.Ok) return Relay.Error(reply.Error);

        var path = reply.Result!["path"]!.GetValue<string>();
        var bytes = await File.ReadAllBytesAsync(path, cancel);
        File.Delete(path);
        // ImageContentBlock.Data is a ReadOnlyMemory<byte> of already-base64-encoded bytes in this SDK
        // version; FromBytes takes the raw decoded bytes and encodes lazily, so it is the simpler path.
        return new CallToolResult
        {
            Content = [ImageContentBlock.FromBytes(bytes, "image/png")],
        };
    }

    [McpServerTool(Name = "teleport"), Description("Move the player to x,y,z, or in front of a shelf/slot handle. " +
        "distance is how far in front (negative if the player lands behind it).")]
    public static Task<CallToolResult> Teleport(BridgeClient bridge, string target = null, float? x = null, float? y = null,
        float? z = null, float distance = 1.5f, CancellationToken cancel = default) =>
        Relay.Run(bridge, "teleport", new { target, x, y, z, distance }, cancel: cancel);

    [McpServerTool(Name = "look_at"), Description("Turn the player and camera toward x,y,z or a handle.")]
    public static Task<CallToolResult> LookAt(BridgeClient bridge, string target = null, float? x = null, float? y = null,
        float? z = null, CancellationToken cancel = default) =>
        Relay.Run(bridge, "look_at", new { target, x, y, z }, cancel: cancel);
}

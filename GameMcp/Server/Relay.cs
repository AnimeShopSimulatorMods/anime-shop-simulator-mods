using System.Text.Json;
using ModelContextProtocol.Protocol;

namespace GameMcp;

// Turns a bridge reply into what an MCP tool returns. Errors are tool errors (IsError), not
// exceptions, so Claude sees the game's own message instead of a protocol failure.
public static class Relay
{
    private static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true };

    public static async Task<CallToolResult> Run(BridgeClient bridge, string command, object args,
        int timeoutMs = 10000, CancellationToken cancel = default)
    {
        try
        {
            var reply = await bridge.CallAsync(command, args, timeoutMs, cancel);
            return reply.Ok
                ? Text(reply.Result?.ToJsonString(Pretty) ?? "null")
                : Error(reply.Error);
        }
        catch (BridgeUnavailableException ex)
        {
            return Error(ex.Message);
        }
        catch (OperationCanceledException)
        {
            return Error($"'{command}' got no answer in time. The game may be frozen or loading.");
        }
    }

    public static CallToolResult Text(string text) =>
        new() { Content = [new TextContentBlock { Text = text }] };

    public static CallToolResult Error(string text) =>
        new() { IsError = true, Content = [new TextContentBlock { Text = text }] };
}

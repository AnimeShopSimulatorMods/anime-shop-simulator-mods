using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace GameMcp;

public sealed record BridgeReply(bool Ok, JsonNode Result, string Error);

public sealed class BridgeUnavailableException(string message) : Exception(message);

// One short-lived connection per call. Keeping no connection open means a game restart needs no
// reconnect logic: the next call simply finds the new bridge.
public sealed class BridgeClient(int port)
{
    private const int ConnectTimeoutMs = 1500;
    private const int ExtraReplyMs = 5000;
    private static int _nextId;

    public int Port => port;

    public async Task<BridgeReply> CallAsync(string command, object args, int timeoutMs = 10000,
        CancellationToken cancel = default)
    {
        using var client = new TcpClient();
        try
        {
            using var connect = CancellationTokenSource.CreateLinkedTokenSource(cancel);
            connect.CancelAfter(ConnectTimeoutMs);
            await client.ConnectAsync("127.0.0.1", port, connect.Token);
        }
        catch (Exception ex) when (ex is SocketException or OperationCanceledException)
        {
            throw new BridgeUnavailableException(
                $"The game is not running, or the GameBridge mod is not installed (nothing listening on 127.0.0.1:{port}).");
        }

        var request = new JsonObject
        {
            ["id"] = Interlocked.Increment(ref _nextId),
            ["cmd"] = command,
            ["args"] = args == null ? null : JsonSerializer.SerializeToNode(args),
            ["timeoutMs"] = timeoutMs,
        };

        var stream = client.GetStream();
        var bytes = Encoding.UTF8.GetBytes(request.ToJsonString() + "\n");
        await stream.WriteAsync(bytes, cancel);

        using var reply = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        reply.CancelAfter(timeoutMs + ExtraReplyMs);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var line = await reader.ReadLineAsync(reply.Token)
                   ?? throw new BridgeUnavailableException("The game closed the connection before answering.");

        var node = JsonNode.Parse(line)!;
        return new BridgeReply(
            node["ok"]?.GetValue<bool>() ?? false,
            node["result"],
            node["error"]?.GetValue<string>());
    }
}

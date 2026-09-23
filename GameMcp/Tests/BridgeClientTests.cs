using GameBridge.Net;
using GameMcp;
using Xunit;

namespace GameMcp.Tests;

public sealed class BridgeClientTests
{
    [Fact]
    public async Task Round_trip_through_the_real_bridge_server()
    {
        var dispatcher = new Dispatcher();
        dispatcher.Register("echo", args => new { said = (string)args["text"] });
        var server = new LineServer(dispatcher, 0);
        server.Start();
        using var stop = new CancellationTokenSource();
        var pump = Task.Run(() => { while (!stop.IsCancellationRequested) { dispatcher.Pump(8); Thread.Sleep(2); } });

        try
        {
            var reply = await new BridgeClient(server.Port).CallAsync("echo", new { text = "hello" });

            Assert.True(reply.Ok);
            Assert.Equal("hello", reply.Result!["said"]!.GetValue<string>());
        }
        finally
        {
            stop.Cancel();
            await pump;
            server.Stop();
        }
    }

    [Fact]
    public async Task Closed_port_says_the_game_is_not_running()
    {
        var ex = await Assert.ThrowsAsync<BridgeUnavailableException>(
            () => new BridgeClient(1).CallAsync("ping", null));

        Assert.Contains("not running", ex.Message);
        Assert.Contains("1", ex.Message);
    }

    [Fact]
    public async Task Relay_formats_unavailable_as_a_tool_error()
    {
        var result = await Relay.Run(new BridgeClient(1), "ping", null);

        Assert.True(result.IsError);
    }
}

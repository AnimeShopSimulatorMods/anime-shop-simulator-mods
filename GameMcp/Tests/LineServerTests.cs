using System.Net;
using System.Net.Sockets;
using System.Text;
using GameBridge.Net;
using Newtonsoft.Json.Linq;
using Xunit;

namespace GameMcp.Tests;

// Runs the bridge's real listener and dispatcher, with a background loop standing in for Unity's
// main thread, and talks to it over a real socket.
public sealed class LineServerTests : IDisposable
{
    private readonly Dispatcher _dispatcher = new Dispatcher();
    private readonly LineServer _server;
    private readonly CancellationTokenSource _stop = new CancellationTokenSource();
    private readonly Task _mainThread;

    public LineServerTests()
    {
        _server = new LineServer(_dispatcher, port: 0);
        _server.Start();
        _mainThread = Task.Run(() =>
        {
            while (!_stop.IsCancellationRequested)
            {
                _dispatcher.Pump(8);
                Thread.Sleep(2);
            }
        });
    }

    public void Dispose()
    {
        _stop.Cancel();
        _mainThread.Wait(1000);
        _server.Stop();
    }

    private JObject Send(string line)
    {
        using var client = new TcpClient();
        client.Connect(IPAddress.Loopback, _server.Port);
        using var stream = client.GetStream();
        var bytes = Encoding.UTF8.GetBytes(line + "\n");
        stream.Write(bytes, 0, bytes.Length);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return JObject.Parse(reader.ReadLine());
    }

    [Fact]
    public void Sync_command_replies_with_its_result()
    {
        _dispatcher.Register("echo", args => new { said = (string)args["text"] });

        var reply = Send("{\"id\":1,\"cmd\":\"echo\",\"args\":{\"text\":\"hi\"}}");

        Assert.Equal(1, (int)reply["id"]);
        Assert.True((bool)reply["ok"]);
        Assert.Equal("hi", (string)reply["result"]["said"]);
    }

    [Fact]
    public void Unknown_command_is_an_error()
    {
        var reply = Send("{\"id\":2,\"cmd\":\"nope\"}");

        Assert.False((bool)reply["ok"]);
        Assert.Contains("Unknown command 'nope'", (string)reply["error"]);
    }

    [Fact]
    public void Throwing_handler_becomes_an_error_reply()
    {
        _dispatcher.Register("boom", _ => throw new InvalidOperationException("kaput"));

        var reply = Send("{\"id\":3,\"cmd\":\"boom\"}");

        Assert.False((bool)reply["ok"]);
        Assert.Contains("InvalidOperationException", (string)reply["error"]);
        Assert.Contains("kaput", (string)reply["error"]);
    }

    [Fact]
    public void Handler_that_never_replies_times_out()
    {
        _dispatcher.Register("silent", (Request _) => { });

        var reply = Send("{\"id\":4,\"cmd\":\"silent\",\"timeoutMs\":200}");

        Assert.False((bool)reply["ok"]);
        Assert.Contains("timed out", (string)reply["error"]);
    }

    [Fact]
    public void Deferred_handler_replies_after_later_frames()
    {
        _dispatcher.Register("later", (Request request) =>
        {
            int frames = 0;
            _dispatcher.Defer(() =>
            {
                if (++frames < 3) return false;
                request.Reply(new { frames });
                return true;
            });
        });

        var reply = Send("{\"id\":5,\"cmd\":\"later\"}");

        Assert.True((bool)reply["ok"]);
        Assert.Equal(3, (int)reply["result"]["frames"]);
    }

    [Fact]
    public void Malformed_json_is_an_error_not_a_dropped_connection()
    {
        var reply = Send("{not json");

        Assert.False((bool)reply["ok"]);
        Assert.Contains("Bad request", (string)reply["error"]);
    }
}

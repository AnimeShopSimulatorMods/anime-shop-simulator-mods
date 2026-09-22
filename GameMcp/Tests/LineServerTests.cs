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

    // Lets a test suspend the stand-in main thread mid-request, so a command can be shown to time
    // out on the client before the "game" ever gets to run it.
    private volatile bool _paused;

    public LineServerTests()
    {
        _server = new LineServer(_dispatcher, port: 0);
        _server.Start();
        _mainThread = Task.Run(() =>
        {
            while (!_stop.IsCancellationRequested)
            {
                if (!_paused) _dispatcher.Pump(8);
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
        // A regression that drops the connection or forgets to reply should fail the test outright
        // instead of hanging the test run.
        client.ReceiveTimeout = 5000;
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
            _dispatcher.Defer(request, () =>
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

    [Fact]
    public void Wrong_typed_id_is_an_error_not_a_dropped_connection()
    {
        var reply = Send("{\"id\":\"abc\",\"cmd\":\"x\"}");

        Assert.False((bool)reply["ok"]);
        Assert.Contains("Bad request", (string)reply["error"]);
    }

    [Fact]
    public void Wrong_typed_cmd_is_an_error_not_a_dropped_connection()
    {
        var reply = Send("{\"id\":1,\"cmd\":{}}");

        Assert.False((bool)reply["ok"]);
        Assert.Contains("Bad request", (string)reply["error"]);
    }

    [Fact]
    public void Timed_out_command_is_not_run_when_the_main_thread_catches_up()
    {
        int calls = 0;
        _dispatcher.Register("count", (Request request) =>
        {
            Interlocked.Increment(ref calls);
            request.Reply(new { });
        });

        // Pause the stand-in main thread so the request sits in the queue past its own timeout,
        // exactly like a frozen or loading game would leave it.
        _paused = true;
        var reply = Send("{\"id\":6,\"cmd\":\"count\",\"timeoutMs\":200}");

        Assert.False((bool)reply["ok"]);
        Assert.Contains("timed out", (string)reply["error"]);

        _paused = false;
        Thread.Sleep(500);

        Assert.Equal(0, calls);
    }

    [Fact]
    public void Throwing_ticker_fails_its_request_instead_of_timing_out()
    {
        _dispatcher.Register("later-boom", (Request request) =>
        {
            _dispatcher.Defer(request, () => throw new InvalidOperationException("tick boom"));
        });

        var reply = Send("{\"id\":7,\"cmd\":\"later-boom\",\"timeoutMs\":5000}");

        Assert.False((bool)reply["ok"]);
        Assert.Contains("tick boom", (string)reply["error"]);
        Assert.DoesNotContain("timed out", (string)reply["error"]);
    }
}

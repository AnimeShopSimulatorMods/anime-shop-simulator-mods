using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GameBridge.Net
{
    // Newline-delimited JSON over TCP, loopback only. One request per line, one reply per line, in
    // order. Each connection gets its own background thread; a request waits here, not on the main
    // thread, so a slow command never stalls the game.
    public sealed class LineServer
    {
        private readonly Dispatcher _dispatcher;
        private readonly int _requestedPort;
        private TcpListener _listener;
        private Thread _acceptThread;
        private volatile bool _running;

        public Action<string> LogError = _ => { };

        public LineServer(Dispatcher dispatcher, int port)
        {
            _dispatcher = dispatcher;
            _requestedPort = port;
        }

        public int Port { get; private set; }

        public void Start()
        {
            _listener = new TcpListener(IPAddress.Loopback, _requestedPort);
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _running = true;
            _acceptThread = new Thread(AcceptLoop) { IsBackground = true, Name = "GameBridge accept" };
            _acceptThread.Start();
        }

        public void Stop()
        {
            _running = false;
            try
            {
                _listener?.Stop();
            }
            catch
            {
                // Already closed.
            }
        }

        private void AcceptLoop()
        {
            while (_running)
            {
                TcpClient client;
                try
                {
                    client = _listener.AcceptTcpClient();
                }
                catch
                {
                    if (!_running) return;
                    // A transient accept failure (e.g. a half-open connection resetting) would
                    // otherwise spin this loop at full CPU; back off briefly before retrying.
                    Thread.Sleep(100);
                    continue;
                }

                var thread = new Thread(() => Serve(client)) { IsBackground = true, Name = "GameBridge client" };
                thread.Start();
            }
        }

        private void Serve(TcpClient client)
        {
            using (client)
            {
                try
                {
                    var stream = client.GetStream();
                    var reader = new StreamReader(stream, new UTF8Encoding(false));
                    var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" };

                    string line;
                    while (_running && (line = reader.ReadLine()) != null)
                    {
                        if (line.Length == 0) continue;
                        writer.WriteLine(Handle(line).ToString(Formatting.None));
                    }
                }
                catch (IOException)
                {
                    // The client hung up.
                }
                catch (Exception ex)
                {
                    LogError($"[GameBridge] Connection failed: {ex}");
                }
            }
        }

        private JObject Handle(string line)
        {
            JObject message;
            try
            {
                message = JObject.Parse(line);
            }
            catch (Exception ex)
            {
                return new JObject { ["id"] = 0, ["ok"] = false, ["error"] = $"Bad request: {ex.Message}" };
            }

            // "id", "cmd", and "timeoutMs" can each be present but the wrong JSON type (a string
            // where a number is expected, an object where a string is expected, a number too large
            // for Int32). Newtonsoft's Value<T> throws in that case; catching it and replying keeps
            // the connection alive instead of tearing it down with no reply at all.
            int id;
            try
            {
                id = message.Value<int?>("id") ?? 0;
            }
            catch (Exception ex)
            {
                return new JObject { ["id"] = 0, ["ok"] = false, ["error"] = $"Bad request: invalid 'id': {ex.Message}" };
            }

            string command;
            try
            {
                command = message.Value<string>("cmd");
            }
            catch (Exception ex)
            {
                return new JObject { ["id"] = id, ["ok"] = false, ["error"] = $"Bad request: invalid 'cmd': {ex.Message}" };
            }

            if (string.IsNullOrEmpty(command))
                return new JObject { ["id"] = id, ["ok"] = false, ["error"] = "Bad request: no 'cmd'." };

            int timeoutMs;
            try
            {
                timeoutMs = message.Value<int?>("timeoutMs") ?? 0;
            }
            catch (Exception ex)
            {
                return new JObject { ["id"] = id, ["ok"] = false, ["error"] = $"Bad request: invalid 'timeoutMs': {ex.Message}" };
            }

            var request = new Request(id, command, message["args"] as JObject, timeoutMs);
            _dispatcher.Enqueue(request);

            if (request.Wait()) return request.Response;

            // Answer once so a late reply from the main thread is dropped instead of written.
            request.Fail($"'{command}' timed out after {request.TimeoutMs} ms. " +
                         "The game may be frozen, loading, or paused in the background.");
            // Fail can lose the race to a main-thread Reply that landed at the same instant: the
            // winner flips _answered before it finishes writing Response, so read Response only
            // after WaitAnswered confirms the winning call has actually written it.
            request.WaitAnswered();
            return request.Response;
        }
    }
}

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

            int id = message.Value<int?>("id") ?? 0;
            var command = message.Value<string>("cmd");
            if (string.IsNullOrEmpty(command))
                return new JObject { ["id"] = id, ["ok"] = false, ["error"] = "Bad request: no 'cmd'." };

            var request = new Request(id, command, message["args"] as JObject, message.Value<int?>("timeoutMs") ?? 0);
            _dispatcher.Enqueue(request);

            if (request.Wait()) return request.Response;

            // Answer once so a late reply from the main thread is dropped instead of written.
            request.Fail($"'{command}' timed out after {request.TimeoutMs} ms. " +
                         "The game may be frozen, loading, or paused in the background.");
            return request.Response;
        }
    }
}

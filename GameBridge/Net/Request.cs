using System;
using System.Threading;
using Newtonsoft.Json.Linq;

namespace GameBridge.Net
{
    // One command in flight. The network thread creates it and waits; the main thread answers it,
    // either at once or frames later. Whichever answer comes first wins, so a late reply after a
    // timeout is dropped rather than written to a connection that has moved on.
    public sealed class Request
    {
        public const int DefaultTimeoutMs = 10000;
        public const int MaxTimeoutMs = 660000;

        private readonly ManualResetEventSlim _done = new ManualResetEventSlim(false);
        private int _answered;

        public Request(int id, string command, JObject args, int timeoutMs)
        {
            Id = id;
            Command = command;
            Args = args ?? new JObject();
            TimeoutMs = timeoutMs <= 0 ? DefaultTimeoutMs : Math.Min(timeoutMs, MaxTimeoutMs);
        }

        public int Id { get; }
        public string Command { get; }
        public JObject Args { get; }
        public int TimeoutMs { get; }
        public JObject Response { get; private set; }

        public void Reply(object result)
        {
            Answer(new JObject
            {
                ["id"] = Id,
                ["ok"] = true,
                ["result"] = result == null ? JValue.CreateNull() : JToken.FromObject(result),
            });
        }

        public void Fail(string error)
        {
            Answer(new JObject { ["id"] = Id, ["ok"] = false, ["error"] = error });
        }

        public bool Wait() => _done.Wait(TimeoutMs);

        private void Answer(JObject response)
        {
            if (Interlocked.Exchange(ref _answered, 1) != 0) return;
            Response = response;
            _done.Set();
        }
    }
}

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace GameBridge.Net
{
    // The only way work reaches the game. Il2Cpp objects must not be touched off the main thread, so
    // the network thread only enqueues; Pump runs on the main thread once per frame.
    //
    // Commands that need to span frames (waiting, holding a key, a screenshot being written) register
    // a ticker with Defer. A ticker returns true when it is finished and has answered its request.
    public sealed class Dispatcher
    {
        public delegate void Handler(Request request);

        private readonly Dictionary<string, Handler> _handlers = new Dictionary<string, Handler>(StringComparer.Ordinal);
        private readonly ConcurrentQueue<Request> _queue = new ConcurrentQueue<Request>();
        private readonly List<Func<bool>> _tickers = new List<Func<bool>>();

        public Action<string> LogError = _ => { };

        public IEnumerable<string> Commands => _handlers.Keys;

        public void Register(string name, Handler handler) => _handlers[name] = handler;

        public void Register(string name, Func<JObject, object> handler) =>
            _handlers[name] = request => request.Reply(handler(request.Args));

        public void Enqueue(Request request) => _queue.Enqueue(request);

        public void Defer(Func<bool> ticker) => _tickers.Add(ticker);

        public void Pump(int maxCommands)
        {
            for (int i = 0; i < maxCommands && _queue.TryDequeue(out var request); i++)
                Run(request);

            for (int i = _tickers.Count - 1; i >= 0; i--)
            {
                bool finished;
                try
                {
                    finished = _tickers[i]();
                }
                catch (Exception ex)
                {
                    LogError($"[GameBridge] A deferred command failed: {ex}");
                    finished = true;
                }
                if (finished) _tickers.RemoveAt(i);
            }
        }

        private void Run(Request request)
        {
            if (!_handlers.TryGetValue(request.Command, out var handler))
            {
                request.Fail($"Unknown command '{request.Command}'.");
                return;
            }

            try
            {
                handler(request);
            }
            catch (Exception ex)
            {
                var inner = ex is System.Reflection.TargetInvocationException tie && tie.InnerException != null
                    ? tie.InnerException
                    : ex;
                request.Fail($"{inner.GetType().Name}: {inner.Message}\n{inner.StackTrace}");
            }
        }
    }
}

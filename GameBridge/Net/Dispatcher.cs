using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace GameBridge.Net
{
    // The only way work reaches the game. Il2Cpp objects must not be touched off the main thread, so
    // the network thread only enqueues; Pump runs on the main thread once per frame.
    //
    // Commands that need to span frames (waiting, holding a key, a screenshot being written) register
    // a ticker with Defer. A ticker returns true when it is finished and has answered its request.
    //
    // Threading: Register is not thread-safe and must be called before the server starts accepting
    // connections. Defer, Pump, and Commands are main-thread-only; nothing else should call them
    // concurrently with Pump.
    public sealed class Dispatcher
    {
        public delegate void Handler(Request request);

        private readonly Dictionary<string, Handler> _handlers = new Dictionary<string, Handler>(StringComparer.Ordinal);
        private readonly ConcurrentQueue<Request> _queue = new ConcurrentQueue<Request>();
        private readonly List<(Request Request, Func<bool> Ticker)> _tickers = new List<(Request, Func<bool>)>();

        public Action<string> LogError = _ => { };

        // A snapshot, not a live view, so handing this out never risks a collection-modified
        // exception if a caller enumerates it while Register runs.
        public string[] Commands => _handlers.Keys.ToArray();

        public void Register(string name, Handler handler) => _handlers[name] = handler;

        public void Register(string name, Func<JObject, object> handler) =>
            _handlers[name] = request => request.Reply(handler(request.Args));

        public void Enqueue(Request request) => _queue.Enqueue(request);

        public void Defer(Func<bool> ticker) => _tickers.Add((null, ticker));

        // Same as Defer(Func<bool>), but ties the ticker to the request it will eventually answer.
        // If the ticker throws, this request fails immediately instead of the client waiting out the
        // full timeout for a command whose game-side execution already blew up.
        public void Defer(Request request, Func<bool> ticker) => _tickers.Add((request, ticker));

        public void Pump(int maxCommands)
        {
            for (int i = 0; i < maxCommands && _queue.TryDequeue(out var request); i++)
                Run(request);

            for (int i = _tickers.Count - 1; i >= 0; i--)
            {
                var (request, ticker) = _tickers[i];
                bool finished;
                try
                {
                    finished = ticker();
                }
                catch (Exception ex)
                {
                    var inner = ex is System.Reflection.TargetInvocationException tie && tie.InnerException != null
                        ? tie.InnerException
                        : ex;
                    LogError($"[GameBridge] A deferred command failed: {inner}");
                    request?.Fail($"{inner.GetType().Name}: {inner.Message}");
                    finished = true;
                }
                if (finished) _tickers.RemoveAt(i);
            }
        }

        private void Run(Request request)
        {
            // The client already gave up and got a timeout reply; running the handler now would
            // produce side effects the caller was told never happened.
            if (request.IsAnswered) return;

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

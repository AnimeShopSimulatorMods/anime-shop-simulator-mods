using System.Collections.Generic;
using GameBridge.Net;
using GameBridge.Reflection;
using Il2CppInterop.Runtime;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace GameBridge.Commands
{
    internal static class ReflectionCommands
    {
        public static void Register(Dispatcher d)
        {
            d.Register("find_objects", FindObjects);
            d.Register("get", args =>
            {
                var (instance, type) = Members.Target((string)args["target"]);
                return JsonValues.ToJson(Members.Get(instance, type, (string)args["path"]));
            });
            d.Register("set", args =>
            {
                var (instance, type) = Members.Target((string)args["target"]);
                Members.Set(instance, type, (string)args["path"], args["value"]);
                return JsonValues.ToJson(Members.Get(instance, type, (string)args["path"]));
            });
            d.Register("call", args =>
            {
                var (instance, type) = Members.Target((string)args["target"]);
                return JsonValues.ToJson(Members.Call(instance, type, (string)args["method"], args["args"] as JArray));
            });
            // Sugar for static methods in mods, e.g. SmartRestockEmployees.ShelfLocks.LockEmptySlots.
            d.Register("mod_call", args =>
            {
                var type = TypeResolver.Resolve((string)args["type"]);
                return JsonValues.ToJson(Members.Call(null, type, (string)args["method"], args["args"] as JArray));
            });
        }

        private static object FindObjects(JObject args)
        {
            var type = TypeResolver.Resolve((string)args["type"]);
            int limit = args.Value<int?>("limit") ?? 50;

            var found = Object.FindObjectsOfType(Il2CppType.From(type));
            var result = new List<object>();
            for (int i = 0; i < found.Length && result.Count < limit; i++)
            {
                var native = found[i];
                if (native == null) continue;
                var wrapped = JsonValues.Cast(native, type);
                var entry = new Dictionary<string, object>
                {
                    ["handle"] = Handles.Add(wrapped),
                    ["name"] = native.name,
                };
                if (wrapped is Component component) entry["position"] = JsonValues.Vec(component.transform.position);
                result.Add(entry);
            }
            return new { type = type.FullName, total = found.Length, objects = result };
        }
    }
}

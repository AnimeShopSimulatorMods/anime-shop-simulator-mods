using System;
using System.Collections.Generic;
using System.Reflection;
using Il2CppInterop.Runtime.InteropTypes;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace GameBridge.Reflection
{
    // What crosses the wire. Plain values go as JSON; anything with identity goes as a handle, with a
    // short description so a listing is readable without a follow-up call per object.
    internal static class JsonValues
    {
        public static object ToJson(object value)
        {
            switch (value)
            {
                case null: return null;
                case string _: case bool _: case int _: case long _: case float _: case double _:
                case short _: case byte _: case uint _: case ulong _: case decimal _:
                    return value;
                case Enum e: return e.ToString();
                case Vector3 v: return Vec(v);
                case Vector2 v2: return new { x = v2.x, y = v2.y };
                case Quaternion q: return new { euler = Vec(q.eulerAngles) };
                case Color c: return new { r = c.r, g = c.g, b = c.b, a = c.a };
                case Guid g: return g.ToString();
                case Il2CppSystem.Guid ig: return ig.ToString();
            }

            return new Dictionary<string, object>
            {
                ["handle"] = Handles.Add(value),
                ["type"] = value.GetType().FullName,
                ["name"] = NameOf(value),
                ["text"] = SafeToString(value),
            };
        }

        public static object Vec(Vector3 v) => new { x = Round(v.x), y = Round(v.y), z = Round(v.z) };

        // Primitive-valued properties of an object, one level deep. For listings such as employee info.
        public static Dictionary<string, object> Snapshot(object value)
        {
            var result = new Dictionary<string, object>();
            if (value == null) return result;

            foreach (var property in value.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.GetIndexParameters().Length > 0) continue;
                var type = property.PropertyType;
                if (!(type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(Vector3))) continue;
                try
                {
                    result[property.Name] = ToJson(property.GetValue(value));
                }
                catch
                {
                    // Some interop getters throw on objects in the wrong state; skip them.
                }
            }
            return result;
        }

        public static object FromJson(JToken token, Type target)
        {
            if (token == null || token.Type == JTokenType.Null) return null;

            if (token.Type == JTokenType.String && Handles.IsHandle((string)token))
                return Cast(Handles.Get((string)token), target);

            if (target.IsEnum)
                return token.Type == JTokenType.String
                    ? Enum.Parse(target, (string)token, true)
                    : Enum.ToObject(target, (long)token);

            if (target == typeof(Vector3))
                return new Vector3((float)token["x"], (float)token["y"], (float)token["z"]);

            if (target == typeof(object)) return ((JValue)token).Value;

            return token.ToObject(target);
        }

        // Interop wrappers are views over a native pointer, so "casting" means wrapping the same
        // pointer in the wanted wrapper type.
        public static object Cast(object value, Type target)
        {
            if (value == null || target.IsInstanceOfType(value)) return value;
            if (value is Il2CppObjectBase native && typeof(Il2CppObjectBase).IsAssignableFrom(target))
                return Activator.CreateInstance(target, native.Pointer);
            throw new ArgumentException($"A {value.GetType().Name} cannot be used as {target.Name}.");
        }

        public static string NameOf(object value)
        {
            try
            {
                if (value is Component component && component != null) return component.gameObject.name;
                if (value is UnityEngine.Object unityObject && unityObject != null) return unityObject.name;
            }
            catch
            {
                // Destroyed objects throw here.
            }
            return null;
        }

        private static string SafeToString(object value)
        {
            try
            {
                var text = value.ToString();
                return text != null && text.Length > 200 ? text.Substring(0, 200) + "..." : text;
            }
            catch
            {
                return null;
            }
        }

        private static float Round(float f) => (float)Math.Round(f, 3);
    }
}

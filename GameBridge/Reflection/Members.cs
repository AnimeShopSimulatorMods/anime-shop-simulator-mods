using System;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace GameBridge.Reflection
{
    // Get, set and call by name. Il2CppInterop exposes the game's fields as properties, so properties
    // are tried first; private members are included because the interesting state is usually private.
    internal static class Members
    {
        private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic |
                                         BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy;

        // "h:12" for an object, or "type:Full.Name" for static members.
        public static (object instance, Type type) Target(string target)
        {
            if (string.IsNullOrEmpty(target)) throw new ArgumentException("No target given.");
            if (target.StartsWith("type:", StringComparison.Ordinal))
                return (null, TypeResolver.Resolve(target.Substring(5)));
            var instance = Handles.Get(target);
            return (instance, instance.GetType());
        }

        // Dotted path, e.g. "_productPlace.Count".
        public static object Get(object instance, Type type, string path)
        {
            foreach (var part in path.Split('.'))
            {
                instance = Read(instance, type, part);
                type = instance?.GetType();
                if (instance == null) break;
            }
            return instance;
        }

        public static void Set(object instance, Type type, string path, JToken value)
        {
            var parts = path.Split('.');
            for (int i = 0; i < parts.Length - 1; i++)
            {
                instance = Read(instance, type, parts[i]);
                if (instance == null) throw new NullReferenceException($"'{parts[i]}' is null.");
                type = instance.GetType();
            }

            var last = parts[parts.Length - 1];
            var property = type.GetProperty(last, All);
            if (property != null && property.CanWrite)
            {
                property.SetValue(instance, JsonValues.FromJson(value, property.PropertyType));
                return;
            }
            var field = type.GetField(last, All);
            if (field != null)
            {
                field.SetValue(instance, JsonValues.FromJson(value, field.FieldType));
                return;
            }
            throw new MissingMemberException($"{type.FullName} has no writable '{last}'.");
        }

        public static object Call(object instance, Type type, string name, JArray args)
        {
            args = args ?? new JArray();
            var candidates = type.GetMethods(All)
                .Where(m => m.Name == name && m.GetParameters().Length == args.Count && !m.IsGenericMethodDefinition)
                .ToList();
            if (candidates.Count == 0)
                throw new MissingMethodException($"{type.FullName} has no method '{name}' taking {args.Count} argument(s).");

            Exception lastError = null;
            foreach (var method in candidates)
            {
                object[] converted;
                try
                {
                    var parameters = method.GetParameters();
                    converted = parameters.Select((p, i) => JsonValues.FromJson(args[i], p.ParameterType)).ToArray();
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    continue;
                }
                return method.Invoke(method.IsStatic ? null : instance, converted);
            }
            throw new ArgumentException($"No overload of '{name}' accepts those arguments: {lastError?.Message}");
        }

        private static object Read(object instance, Type type, string name)
        {
            var property = type.GetProperty(name, All);
            if (property != null && property.GetIndexParameters().Length == 0) return property.GetValue(instance);
            var field = type.GetField(name, All);
            if (field != null) return field.GetValue(instance);
            throw new MissingMemberException($"{type.FullName} has no member '{name}'.");
        }
    }
}

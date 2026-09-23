using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace GameBridge.Reflection
{
    // Finds a type across every loaded assembly -- the game's interop assemblies and every mod -- by
    // full name, or by short name when that is unambiguous.
    //
    // Every mod compiles Shared/ into itself, so a type like AnimeShopMods.Game.ShelfMemory exists once
    // per mod. "SmartRestockEmployees:AnimeShopMods.Game.ShelfMemory" picks the copy in one assembly.
    internal static class TypeResolver
    {
        private static readonly Dictionary<string, Type> Cache = new Dictionary<string, Type>(StringComparer.Ordinal);

        public static Type Resolve(string name)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("No type name given.");
            if (Cache.TryGetValue(name, out var cached)) return cached;

            string assemblyName = null;
            string typeName = name;
            int colon = name.IndexOf(':');
            if (colon > 0)
            {
                assemblyName = name.Substring(0, colon);
                typeName = name.Substring(colon + 1);
            }

            var all = AllTypes().Where(t => assemblyName == null || t.Assembly.GetName().Name == assemblyName).ToList();
            var exact = all.Where(t => t.FullName == typeName).ToList();
            var matches = exact.Count > 0 ? exact : all.Where(t => t.Name == typeName).ToList();

            if (matches.Count == 0)
                throw new ArgumentException($"No loaded type is called '{name}'.");
            if (matches.Count > 1)
                throw new ArgumentException($"'{name}' is ambiguous: " +
                                            string.Join(", ", matches.Take(10).Select(t => $"{t.Assembly.GetName().Name}:{t.FullName}")) +
                                            ". Use one of these.");

            Cache[name] = matches[0];
            return matches[0];
        }

        private static IEnumerable<Type> AllTypes()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types;
                }
                catch
                {
                    continue;
                }

                foreach (var type in types)
                    if (type != null) yield return type;
            }
        }
    }
}

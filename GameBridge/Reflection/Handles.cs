using System;
using System.Collections.Generic;

namespace GameBridge.Reflection
{
    // Game objects cannot cross the wire, so they are parked here and referred to as "h:12".
    // Everything is dropped on scene unload: those objects are destroyed, and a handle that silently
    // pointed at a dead object would be worse than one that fails loudly.
    internal static class Handles
    {
        private static readonly Dictionary<int, object> Table = new Dictionary<int, object>();
        private static int _next = 1;
        private static string _clearedBy;

        public static string Add(object value)
        {
            int id = _next++;
            Table[id] = value;
            return "h:" + id;
        }

        public static bool IsHandle(string text) => text != null && text.StartsWith("h:", StringComparison.Ordinal);

        public static object Get(string handle)
        {
            if (!IsHandle(handle) || !int.TryParse(handle.Substring(2), out int id))
                throw new ArgumentException($"'{handle}' is not a handle. Handles look like h:12.");

            if (Table.TryGetValue(id, out var value)) return value;

            throw new ArgumentException(_clearedBy == null
                ? $"Unknown handle {handle}."
                : $"Unknown handle {handle}. Handles were cleared when scene '{_clearedBy}' unloaded; look the object up again.");
        }

        public static void Clear(string sceneName)
        {
            Table.Clear();
            _clearedBy = sceneName;
        }
    }
}

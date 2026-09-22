using System.Collections.Generic;
using GameBridge.Net;
using GameBridge.Reflection;
using MelonLoader;
using Newtonsoft.Json.Linq;

namespace GameBridge.Commands
{
    // Every mod's settings. Setting a value saves MelonPreferences.cfg, so it survives a restart, and
    // mods that read their entries live (as Smart Restock does) pick it up at once.
    internal static class PrefsCommands
    {
        public static void Register(Dispatcher d) => d.Register("mod_prefs", Prefs);

        private static object Prefs(JObject args)
        {
            var categoryName = (string)args["category"];
            if (string.IsNullOrEmpty(categoryName))
            {
                var names = new List<string>();
                foreach (var c in MelonPreferences.Categories) names.Add(c.Identifier);
                return new { categories = names };
            }

            var category = MelonPreferences.GetCategory(categoryName)
                           ?? throw new System.ArgumentException($"No preferences category '{categoryName}'.");

            var entryName = (string)args["entry"];
            if (!string.IsNullOrEmpty(entryName) && args["value"] != null)
            {
                var entry = category.GetEntry(entryName)
                            ?? throw new System.ArgumentException($"'{categoryName}' has no entry '{entryName}'.");
                entry.BoxedValue = JsonValues.FromJson(args["value"], entry.GetReflectedType());
                MelonPreferences.Save();
            }

            var values = new Dictionary<string, object>();
            foreach (var entry in category.Entries) values[entry.Identifier] = JsonValues.ToJson(entry.BoxedValue);
            return new { category = category.Identifier, values };
        }
    }
}

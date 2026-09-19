using System;
using System.Collections.Generic;
using System.Text;
using Il2CppProject.Code.Gameplay.Player.Products;
using MelonLoader;

namespace SmartRestockEmployees
{
    // Temporary instrumentation for the 1.0.5 restock failure: employees are handed a target slot and
    // the game declines to use it, so the mod blacklists the slot until nothing is left to restock.
    // This records one block per FindShelf pass showing exactly which stage of the game's own search
    // bailed out.
    //
    // FindShelf is a synchronous void, so passes cannot interleave and one static record is enough.
    //
    // Delete this file and DiagnosticPatches.cs once the root cause is found.
    internal static class Diagnostics
    {
        private const int MaxSteps = 40;
        private const int MaxBlocks = 200;

        private static readonly List<string> Steps = new List<string>();
        private static bool _open;
        private static int _blocks;
        private static string _employee;
        private static int _havePointsAsked;
        private static int _havePointsDenied;
        private static int _shelfAsked;
        private static int _shelfDenied;

        // Hot call sites check this before building a string they may not be able to record.
        public static bool Wants => _open && Steps.Count < MaxSteps;

        public static void Reset()
        {
            _open = false;
            _blocks = 0;
            Steps.Clear();
        }

        public static void Begin(string employee)
        {
            if (!Main.DiagnosticLogs || _blocks >= MaxBlocks) return;

            _open = true;
            _employee = employee;
            Steps.Clear();
            _havePointsAsked = 0;
            _havePointsDenied = 0;
            _shelfAsked = 0;
            _shelfDenied = 0;
        }

        public static void Step(string text)
        {
            if (!_open) return;
            if (Steps.Count == MaxSteps) Steps.Add("... more steps suppressed");
            if (Steps.Count >= MaxSteps) return;
            Steps.Add(text);
        }

        public static void HavePoints(bool allowed)
        {
            if (!_open) return;
            _havePointsAsked++;
            if (!allowed) _havePointsDenied++;
        }

        public static void ShelfCheck(bool allowed)
        {
            if (!_open) return;
            _shelfAsked++;
            if (!allowed) _shelfDenied++;
        }

        public static void End(bool gotPickup, ProductPricePlace chosen)
        {
            if (!_open) return;
            _open = false;
            _blocks++;

            var text = new StringBuilder();
            text.AppendLine($"[Diag] ===== FindShelf {_employee} =====");
            for (int i = 0; i < Steps.Count; i++)
                text.AppendLine("[Diag]   " + Steps[i]);
            text.AppendLine($"[Diag]   HasAvailableShelfPlace: {_shelfAsked} asked, {_shelfDenied} said no");
            text.AppendLine($"[Diag]   HavePoints: {_havePointsAsked} asked, {_havePointsDenied} hidden by this mod");
            text.Append($"[Diag]   RESULT pickup={(gotPickup ? "yes" : "NO")} place={Describe(chosen)}");
            MelonLogger.Msg(text.ToString());

            if (_blocks == MaxBlocks)
                MelonLogger.Msg($"[Diag] {MaxBlocks} blocks recorded; stopping so the log stays readable.");
        }

        public static string Describe(ProductPricePlace place)
        {
            if (place == null) return "null";
            try
            {
                var productPlace = place.ProductPlace;
                int count = productPlace == null ? -1 : productPlace.Count;
                int max = productPlace == null ? -1 : productPlace.MaxCount;
                int last = place.LastDefinitionId;
                string memory = last > 0 ? "remembers " + last : "NEVER STOCKED";
                return $"{place.gameObject.name}[{memory}, {count}/{max}]";
            }
            catch (Exception ex)
            {
                return $"<unreadable: {ex.GetType().Name}>";
            }
        }

        public static string Name(UnityEngine.Component component)
        {
            try
            {
                return component == null ? "?" : component.gameObject.name;
            }
            catch
            {
                return "?";
            }
        }
    }
}

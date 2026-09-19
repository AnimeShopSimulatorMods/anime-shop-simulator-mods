using System;
using System.Collections.Generic;
using AnimeShopMods.Game;
using AnimeShopMods.Ui;
using Il2CppProject.Code.Gameplay.Interactions.Shelfs;
using MelonLoader;
using UnityEngine;

namespace SmartRestockEmployees.Ui
{
    // The player-facing panel. Two views: whichever shelf the player is looking at, and a list of
    // every shelf in the store. No engine vocabulary reaches the screen -- no slots, no definition
    // ids, no GameObject names.
    //
    // No GUILayout.Window: it needs a GUI.WindowFunction delegate and Il2Cpp marshalling of those is
    // unreliable. A plain area inside a box gives the same panel.
    internal static class ShelfPanel
    {
        private const float PanelWidth = 420f;

        private static Rect _area = new Rect(40f, 60f, PanelWidth, 520f);
        private static Vector2 _scroll;
        private static bool _listView;

        private static List<ShelfFinder.ShelfEntry> _shelves;
        private static float _nextScan;

        // The shelf the player picked out of the list. Null means "whatever I am looking at".
        private static ShelfProducts _pinned;

        private static ShelfProducts _pending;
        private static bool _pendingSecondZone;
        private static int _pendingItems;
        private static string _status;

        public static void Reset()
        {
            _shelves = null;
            _nextScan = 0f;
            _pinned = null;
            _pending = null;
            _status = null;
            _scroll = Vector2.zero;
        }

        public static void Draw()
        {
            if (!Skin.Ready)
            {
                DrawFallback();
                return;
            }

            GUILayout.BeginArea(_area, Skin.PanelBox);
            try
            {
                Header();

                if (_pending != null) Confirmation();
                else if (_listView) ListView();
                else AimView();

                Footer();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[ShelfPanel] Draw failed: {ex}");
            }
            finally
            {
                GUILayout.EndArea();
            }
        }

        private static void Header()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(_listView ? "All shelves" : "Shelf", Skin.Title);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(_listView ? "Look instead" : "See all", Skin.Secondary, GUILayout.Width(112f)))
            {
                _listView = !_listView;
                _pinned = null;
                _status = null;
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(10f);
        }

        private static void AimView()
        {
            var shelf = _pinned != null ? _pinned : ShelfFinder.UnderCrosshair();
            if (shelf == null)
            {
                GUILayout.Label("Walk up to a shelf and look at it.", Skin.Hint);
                return;
            }

            var entry = ShelfFinder.Describe(shelf, GameLinks.Products);
            if (entry == null) return;

            // A shelf picked from the list stays put while the player reads the panel, and keeps
            // glowing so they can see which one it is.
            if (_pinned != null)
            {
                ShelfFinder.Highlight(_pinned);
                GUILayout.BeginHorizontal();
                GUILayout.Label("Picked from the list", Skin.Hint);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Unpin", Skin.Secondary, GUILayout.Width(80f))) _pinned = null;
                GUILayout.EndHorizontal();
                GUILayout.Space(6f);
            }

            ShelfCard(entry);
            GUILayout.Space(12f);
            Actions(entry);
        }

        private static void ListView()
        {
            if (_shelves == null || Time.realtimeSinceStartup > _nextScan)
            {
                _shelves = ShelfFinder.Scan(GameLinks.Products);
                _nextScan = Time.realtimeSinceStartup + 2f;
            }

            if (_shelves.Count == 0)
            {
                GUILayout.Label("No shelves in the store yet.", Skin.Hint);
                return;
            }

            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(300f));
            for (int i = 0; i < _shelves.Count; i++)
            {
                var entry = _shelves[i];
                GUILayout.BeginHorizontal(Skin.RaisedBox);

                Icon(entry, 28f);
                GUILayout.BeginVertical();
                GUILayout.Label(entry.ProductName, Skin.Body);
                GUILayout.Label($"{entry.ItemCount} of {entry.MaxCount} items", Skin.Hint);
                GUILayout.EndVertical();

                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Open", Skin.Secondary, GUILayout.Width(74f)))
                {
                    _pinned = entry.Shelf;
                    _listView = false;
                    _status = null;
                }
                GUILayout.EndHorizontal();

                // Hovering a row lights up the real shelf. Shelves have no name a player would
                // recognise, so this is how they tell one row from another.
                if (Event.current != null && Event.current.type == EventType.Repaint &&
                    GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
                    ShelfFinder.Highlight(entry.Shelf);

                GUILayout.Space(4f);
            }
            GUILayout.EndScrollView();
        }

        private static void ShelfCard(ShelfFinder.ShelfEntry entry)
        {
            GUILayout.BeginHorizontal(Skin.RaisedBox);
            Icon(entry, 40f);
            GUILayout.BeginVertical();
            GUILayout.Label(entry.ProductName, Skin.Body);
            GUILayout.Label($"{entry.ItemCount} of {entry.MaxCount} items", Skin.Hint);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        private static void Actions(ShelfFinder.ShelfEntry entry)
        {
            if (!GameLinks.IsServer)
            {
                GUILayout.Label("Only the host can change shelves.", Skin.Hint);
                return;
            }

            bool hasSecond = DeliveryReturn.HasSecondZone(GameLinks.Orders);

            GUILayout.Label("Empty this shelf", Skin.Hint);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Delivery 1", Skin.Primary)) Ask(entry, false);
            GUILayout.Space(8f);
            if (GUILayout.Button("Delivery 2", hasSecond ? Skin.Primary : Skin.Secondary)) Ask(entry, true);
            GUILayout.EndHorizontal();

            GUILayout.Label(hasSecond
                ? "Items go back into boxes at the zone you pick, and employees stop refilling this shelf."
                : "The second delivery zone is not open yet, so items will go to Delivery 1.", Skin.Hint);

            GUILayout.Space(12f);
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();
            GUILayout.Label("Keep this shelf empty", Skin.Body);
            GUILayout.Label("Forget what used to be here", Skin.Hint);
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Forget", Skin.Secondary, GUILayout.Width(96f)))
            {
                int cleared = ShelfMemory.Clear(entry.Shelf);
                _status = cleared > 0
                    ? "Employees will leave this shelf alone."
                    : "This shelf was already forgotten.";
                _shelves = null;
            }
            GUILayout.EndHorizontal();
        }

        private static void Ask(ShelfFinder.ShelfEntry entry, bool secondZone)
        {
            // Nothing to pack up, so there is nothing to confirm -- just do the part that matters.
            if (entry.ItemCount == 0)
            {
                int cleared = ShelfMemory.Clear(entry.Shelf);
                _status = cleared > 0
                    ? "Nothing to pack up. Employees will leave this shelf alone."
                    : "This shelf is already empty and forgotten.";
                _shelves = null;
                return;
            }

            _pending = entry.Shelf;
            _pendingSecondZone = secondZone;
            _pendingItems = entry.ItemCount;
            _status = null;
        }

        private static void Confirmation()
        {
            bool second = _pendingSecondZone && DeliveryReturn.HasSecondZone(GameLinks.Orders);

            GUILayout.Label("Empty this shelf?", Skin.Title);
            GUILayout.Label($"{_pendingItems} items go back into boxes at Delivery {(second ? 2 : 1)}.", Skin.Hint);
            GUILayout.Space(12f);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Cancel", Skin.Secondary)) _pending = null;
            GUILayout.Space(8f);
            if (GUILayout.Button("Empty it", Skin.Primary))
            {
                var result = DeliveryReturn.Run(_pending, GameLinks.Orders, GameLinks.Products, _pendingSecondZone);
                ShelfMemory.Clear(_pending);
                _status = Describe(result);
                _pending = null;
                _shelves = null;
            }
            GUILayout.EndHorizontal();
        }

        private static string Describe(DeliveryReturn.Result result)
        {
            if (result.ZoneWasFull && result.Returned == 0)
                return "The delivery zone is full, so nothing was moved.";

            string text = $"Packed up {result.Returned} items.";
            if (result.FellBackToZoneOne) text += " The second zone is not open yet, so they went to Delivery 1.";
            if (result.ZoneWasFull) text += " The zone filled up, so the rest stayed on the shelf.";
            if (result.Skipped > 0) text += $" {result.Skipped} items have no box to go back into and stayed put.";
            return text;
        }

        private static void Footer()
        {
            GUILayout.FlexibleSpace();

            if (!string.IsNullOrEmpty(_status))
            {
                GUILayout.Label(_status, Skin.Hint);
                GUILayout.Space(8f);
            }

            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();
            GUILayout.Label("Use never-filled slots", Skin.Body);
            GUILayout.Label("Applies to every shelf in the store", Skin.Hint);
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(Main.FillFreeSlots ? "On" : "Off", Skin.Secondary, GUILayout.Width(68f)))
                Main.SetFillFreeSlots(!Main.FillFreeSlots);
            GUILayout.EndHorizontal();
        }

        private static void Icon(ShelfFinder.ShelfEntry entry, float size)
        {
            var rect = GUILayoutUtility.GetRect(size, size, GUILayout.Width(size), GUILayout.Height(size));
            if (GuiCaps.DrawTexture && entry.ProductIcon != null)
                GUI.DrawTexture(rect, entry.ProductIcon);
            GUILayout.Space(10f);
        }

        // If the skin could not be built at all, this is what the player sees. Controls already knows
        // how to degrade to whatever survived this build's IMGUI stripping.
        private static void DrawFallback()
        {
            GUILayout.BeginArea(_area);
            try
            {
                Controls.Header("Shelf");

                var shelf = _pinned != null ? _pinned : ShelfFinder.UnderCrosshair();
                if (shelf == null)
                {
                    Controls.Label("Walk up to a shelf and look at it.");
                    return;
                }

                var entry = ShelfFinder.Describe(shelf, GameLinks.Products);
                if (entry == null) return;

                Controls.Label($"{entry.ProductName} - {entry.ItemCount} of {entry.MaxCount} items");

                if (!GameLinks.IsServer)
                {
                    Controls.Label("Only the host can change shelves.");
                    return;
                }

                if (Controls.Button("Empty to Delivery 1", 190f)) Ask(entry, false);
                if (Controls.Button("Empty to Delivery 2", 190f)) Ask(entry, true);
                if (Controls.Button("Forget this shelf", 190f)) ShelfMemory.Clear(entry.Shelf);

                Controls.Label($"Use never-filled slots: {(Main.FillFreeSlots ? "on" : "off")}");
                if (Controls.Button(Main.FillFreeSlots ? "Turn off" : "Turn on", 190f))
                    Main.SetFillFreeSlots(!Main.FillFreeSlots);

                if (!string.IsNullOrEmpty(_status)) Controls.Label(_status);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[ShelfPanel] Fallback draw failed: {ex}");
            }
            finally
            {
                GUILayout.EndArea();
            }
        }
    }
}

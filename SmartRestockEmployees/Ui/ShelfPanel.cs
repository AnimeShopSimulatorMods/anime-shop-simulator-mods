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
    // Two rules this file exists to obey, both IMGUI's:
    //
    //   1. A frame runs Layout -> input event -> Repaint, and the set of controls must be identical
    //      in all three or Unity throws "Mismatched LayoutGroup". So nothing that changes the shape
    //      of the panel may happen while drawing: buttons queue their work with Queue() and it runs
    //      at the start of the next Layout pass. The same goes for anything the shape is read from --
    //      the crosshair raycast, the shelf list, whether this is the host -- which is why Snapshot()
    //      samples them once per frame rather than per pass.
    //
    //   2. No GUILayout.Window: it needs a GUI.WindowFunction delegate and Il2Cpp marshalling of
    //      those is unreliable. A plain area inside a box gives the same panel.
    internal static class ShelfPanel
    {
        private const float PanelWidth = 420f;

        private static Rect _area = new Rect(40f, 60f, PanelWidth, 520f);
        private static Vector2 _scroll;
        private static bool _listView;

        // The shelf the player picked out of the list. Null means "whatever I am looking at".
        private static ShelfProducts _pinned;

        private static ShelfProducts _pendingShelf;
        private static bool _pendingSecondZone;
        private static int _pendingItems;
        private static string _status;

        private static Action _queued;

        // Everything the layout is built from, sampled once per frame at the Layout pass.
        private static ShelfFinder.ShelfEntry _shelf;
        private static List<ShelfFinder.ShelfEntry> _shelves;
        private static float _nextScan;
        private static bool _isServer;
        private static bool _hasSecondZone;

        public static void Reset()
        {
            _shelf = null;
            _shelves = null;
            _nextScan = 0f;
            _pinned = null;
            _pendingShelf = null;
            _status = null;
            _queued = null;
            _scroll = Vector2.zero;
        }

        public static void Draw()
        {
            var e = Event.current;
            if (e == null || e.type == EventType.Layout) Snapshot();

            if (!Skin.Ready)
            {
                DrawFallback();
                return;
            }

            GUILayout.BeginArea(_area, Skin.PanelBox);
            try
            {
                Header();

                if (_pendingShelf != null) Confirmation();
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

        // Runs only at the Layout pass, so every pass of the frame sees the same panel.
        private static void Snapshot()
        {
            var work = _queued;
            _queued = null;
            if (work != null)
            {
                try
                {
                    work();
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"[ShelfPanel] Action failed: {ex}");
                }
            }

            try
            {
                _isServer = GameLinks.IsServer;
                _hasSecondZone = DeliveryReturn.HasSecondZone(GameLinks.Orders);

                var shelf = _pinned != null ? _pinned : ShelfFinder.UnderCrosshair();
                _shelf = shelf == null ? null : ShelfFinder.Describe(shelf, GameLinks.Products);

                if (_listView && (_shelves == null || Time.realtimeSinceStartup > _nextScan))
                {
                    _shelves = ShelfFinder.Scan(GameLinks.Products);
                    _nextScan = Time.realtimeSinceStartup + 2f;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[ShelfPanel] Snapshot failed: {ex}");
            }
        }

        private static void Queue(Action work)
        {
            _queued = work;
        }

        private static void Header()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(_listView ? "All shelves" : "Shelf", Skin.Title);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(_listView ? "Look instead" : "See all", Skin.Secondary, GUILayout.Width(112f)))
                Queue(() =>
                {
                    _listView = !_listView;
                    _pinned = null;
                    _status = null;
                    _shelves = null;
                });
            GUILayout.EndHorizontal();
            GUILayout.Space(10f);
        }

        private static void AimView()
        {
            if (_shelf == null)
            {
                GUILayout.Label("Walk up to a shelf and look at it.", Skin.Hint);
                return;
            }

            // A shelf picked from the list stays put while the player reads the panel, and keeps
            // glowing so they can see which one it is.
            if (_pinned != null)
            {
                ShelfFinder.Highlight(_pinned);
                GUILayout.BeginHorizontal();
                GUILayout.Label("Picked from the list", Skin.Hint);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Unpin", Skin.Secondary, GUILayout.Width(80f)))
                    Queue(() => _pinned = null);
                GUILayout.EndHorizontal();
                GUILayout.Space(6f);
            }

            ShelfCard(_shelf);
            GUILayout.Space(12f);
            Actions(_shelf);
        }

        private static void ListView()
        {
            if (_shelves == null || _shelves.Count == 0)
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
                    var picked = entry.Shelf;
                    Queue(() =>
                    {
                        _pinned = picked;
                        _listView = false;
                        _status = null;
                    });
                }
                GUILayout.EndHorizontal();

                // Hovering a row lights up the real shelf. Shelves have no name a player would
                // recognise, so this is how they tell one row from another. Repaint only: the rect is
                // not known during Layout, and highlighting changes nothing about the panel's shape.
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
            if (!_isServer)
            {
                GUILayout.Label("Only the host can change shelves.", Skin.Hint);
                return;
            }

            GUILayout.Label("Empty this shelf", Skin.Hint);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Delivery 1", Skin.Primary)) Ask(entry, false);
            GUILayout.Space(8f);
            if (GUILayout.Button("Delivery 2", _hasSecondZone ? Skin.Primary : Skin.Secondary)) Ask(entry, true);
            GUILayout.EndHorizontal();

            GUILayout.Label(_hasSecondZone
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
                var shelf = entry.Shelf;
                Queue(() =>
                {
                    int cleared = ShelfMemory.Clear(shelf);
                    _status = cleared > 0
                        ? "Employees will leave this shelf alone."
                        : "This shelf was already forgotten.";
                    _shelves = null;
                });
            }
            GUILayout.EndHorizontal();
        }

        private static void Ask(ShelfFinder.ShelfEntry entry, bool secondZone)
        {
            var shelf = entry.Shelf;
            int items = entry.ItemCount;

            Queue(() =>
            {
                // Nothing to pack up, so there is nothing to confirm -- just do the part that matters.
                if (items == 0)
                {
                    int cleared = ShelfMemory.Clear(shelf);
                    _status = cleared > 0
                        ? "Nothing to pack up. Employees will leave this shelf alone."
                        : "This shelf is already empty and forgotten.";
                    _shelves = null;
                    return;
                }

                _pendingShelf = shelf;
                _pendingSecondZone = secondZone;
                _pendingItems = items;
                _status = null;
            });
        }

        private static void Confirmation()
        {
            bool second = _pendingSecondZone && _hasSecondZone;

            GUILayout.Label("Empty this shelf?", Skin.Title);
            GUILayout.Label($"{_pendingItems} items go back into boxes at Delivery {(second ? 2 : 1)}.", Skin.Hint);
            GUILayout.Space(12f);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Cancel", Skin.Secondary)) Queue(() => _pendingShelf = null);
            GUILayout.Space(8f);
            if (GUILayout.Button("Empty it", Skin.Primary))
                Queue(() =>
                {
                    var result = DeliveryReturn.Run(_pendingShelf, GameLinks.Orders, GameLinks.Products,
                        _pendingSecondZone);
                    ShelfMemory.Clear(_pendingShelf);
                    _status = Describe(result);
                    _pendingShelf = null;
                    _shelves = null;
                });
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
                Queue(() => Main.SetFillFreeSlots(!Main.FillFreeSlots));
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

                if (_shelf == null)
                {
                    Controls.Label("Walk up to a shelf and look at it.");
                    return;
                }

                Controls.Label($"{_shelf.ProductName} - {_shelf.ItemCount} of {_shelf.MaxCount} items");

                if (!_isServer)
                {
                    Controls.Label("Only the host can change shelves.");
                    return;
                }

                var entry = _shelf;
                if (Controls.Button("Empty to Delivery 1", 190f)) Ask(entry, false);
                if (Controls.Button("Empty to Delivery 2", 190f)) Ask(entry, true);
                if (Controls.Button("Forget this shelf", 190f))
                {
                    var shelf = entry.Shelf;
                    Queue(() => ShelfMemory.Clear(shelf));
                }

                Controls.Label($"Use never-filled slots: {(Main.FillFreeSlots ? "on" : "off")}");
                if (Controls.Button(Main.FillFreeSlots ? "Turn off" : "Turn on", 190f))
                    Queue(() => Main.SetFillFreeSlots(!Main.FillFreeSlots));

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

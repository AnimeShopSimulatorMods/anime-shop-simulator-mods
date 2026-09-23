using System;
using System.Collections.Generic;
using System.Linq;
using AnimeShopMods.Dev;
using AnimeShopMods.Dev.Cheats;
using AnimeShopMods.Game;
using GameBridge.Net;
using GameBridge.Reflection;
using Il2CppProject.Code.Gameplay.AI.Employee;
using Il2CppProject.Code.Gameplay.Controllers;
using Il2CppProject.Code.Gameplay.Interactions.Shelfs;
using Il2CppProject.Code.Gameplay.Player.Products;
using MelonLoader;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameBridge.Commands
{
    internal static class StateCommands
    {
        public static void Register(Dispatcher d)
        {
            d.Register("game_status", _ => Status());
            d.Register("shelves", Shelves);
            d.Register("employees", _ => Employees());
            d.Register("boxes", Boxes);
        }

        private static object Status()
        {
            int hour = -1, minute = -1;
            bool hasClock = GameAccess.InGame && TimeCheats.TryGetClock(out hour, out minute);
            var player = TestingCheats.LocalPlayer();

            return new
            {
                scene = SceneManager.GetActiveScene().name,
                inGame = GameAccess.InGame,
                isHost = GameAccess.IsServer,
                clock = hasClock ? $"{hour:00}:{minute:00}" : null,
                clockRunning = GameAccess.InGame && TimeCheats.IsRunning,
                clockMultiplier = GameAccess.InGame ? TimeCheats.Multiplier : 0f,
                day = GameAccess.InGame ? ProgressCheats.Get(ParameterType.Day) : 0f,
                money = GameAccess.InGame ? ProgressCheats.Get(ParameterType.Money) : 0f,
                unityTimeScale = Time.timeScale,
                focused = Application.isFocused,
                player = player == null ? null : JsonValues.Vec(player.position),
                mods = MelonMod.RegisteredMelons.Select(m => $"{m.Info.Name} {m.Info.Version}").ToArray(),
            };
        }

        // Every slot, with the facts the restock mods reason about, so a bug report can be checked
        // against what the game actually holds.
        private static object Shelves(JObject args)
        {
            bool onlyEmpty = args.Value<bool?>("onlyEmpty") ?? false;
            var products = GameAccess.Products;
            var result = new List<object>();

            foreach (var shelf in UnityEngine.Object.FindObjectsOfType<ShelfProducts>())
            {
                if (shelf == null) continue;
                var described = ShelfFinder.Describe(shelf, products);
                var slots = new List<object>();
                var places = shelf.Places;
                for (int i = 0; places != null && i < places.Length; i++)
                {
                    var place = places[i];
                    if (place == null) continue;
                    var productPlace = place.ProductPlace;
                    int count = productPlace == null ? 0 : productPlace.Count;
                    if (onlyEmpty && count > 0) continue;

                    slots.Add(new
                    {
                        handle = Handles.Add(place),
                        id = place.PersistentId,
                        product = place.ProductInfo == null ? 0 : place.ProductInfo.DefinitionId,
                        lastDefinitionId = place.LastDefinitionId,
                        bound = ModHooks.HasBinding(place),
                        count,
                        max = productPlace == null ? 0 : productPlace.MaxCount,
                        havePoints = place.HavePoints,
                        gameLocked = place.IsLocked,
                        smartRestockLocked = ModHooks.SmartRestockLocked(place),
                    });
                }
                if (slots.Count == 0 && onlyEmpty) continue;

                result.Add(new
                {
                    handle = Handles.Add(shelf),
                    name = shelf.gameObject.name,
                    product = described?.ProductName,
                    items = described?.ItemCount ?? 0,
                    position = JsonValues.Vec(shelf.transform.position),
                    slots,
                });
            }
            return new { count = result.Count, shelves = result };
        }

        private static object Employees()
        {
            var sorting = new List<object>();
            foreach (var controller in UnityEngine.Object.FindObjectsOfType<EmployeeSortingController>())
            {
                if (controller == null) continue;
                var pickup = controller._pickup;
                var target = controller._productPricePlace;
                sorting.Add(new
                {
                    handle = Handles.Add(controller),
                    name = controller.gameObject.name,
                    position = JsonValues.Vec(controller.transform.position),
                    carrying = pickup == null || pickup.ProductDefinition == null ? 0 : pickup.ProductDefinition.Id,
                    targetSlot = target == null ? null : target.PersistentId,
                    targetSlotHandle = target == null ? null : Handles.Add(target),
                });
            }

            var roster = EmployeeCheats.All().Select((info, i) =>
            {
                var snapshot = JsonValues.Snapshot(info);
                snapshot["index"] = i;
                return snapshot;
            }).ToList();

            return new { roster, sorting };
        }

        private static object Boxes(JObject args)
        {
            bool positions = args.Value<bool?>("positions") ?? false;
            var groups = new Dictionary<int, (string name, List<object> where)>();
            foreach (var pickup in UnityEngine.Object.FindObjectsOfType<PickupProducts>())
            {
                if (pickup == null) continue;
                var definition = pickup.ProductDefinition;
                int id = definition == null ? 0 : definition.Id;
                if (!groups.TryGetValue(id, out var group))
                    group = (definition == null ? "(none)" : definition.Name, new List<object>());
                group.where.Add(positions ? JsonValues.Vec(pickup.transform.position) : null);
                groups[id] = group;
            }

            return groups.Select(pair => new
            {
                product = pair.Key,
                name = pair.Value.name,
                boxes = pair.Value.where.Count,
                positions = positions ? pair.Value.where : null,
            }).OrderBy(g => g.name).ToList();
        }
    }

    // Reads another mod's state without a compile-time reference to it.
    internal static class ModHooks
    {
        private static System.Reflection.MethodInfo _isLocked;
        private static bool _looked;

        public static bool? SmartRestockLocked(ProductPricePlace place)
        {
            if (!_looked)
            {
                _looked = true;
                try
                {
                    _isLocked = TypeResolver.Resolve("SmartRestockEmployees.ShelfLocks").GetMethod("IsLocked");
                }
                catch
                {
                    _isLocked = null;
                }
            }
            return _isLocked == null ? (bool?)null : (bool)_isLocked.Invoke(null, new object[] { place });
        }

        public static bool HasBinding(ProductPricePlace place)
        {
            try
            {
                return place._productId.Value != default(Il2CppSystem.Guid);
            }
            catch
            {
                return false;
            }
        }
    }
}

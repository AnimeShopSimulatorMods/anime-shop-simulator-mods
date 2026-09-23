using System;
using System.Linq;
using AnimeShopMods.Dev;
using AnimeShopMods.Dev.Cheats;
using GameBridge.Net;
using GameBridge.Reflection;
using Il2CppProject.Code.Gameplay.AI.Buyer;
using Il2CppProject.Code.Gameplay.Configs;
using Il2CppProject.Code.Gameplay.Controllers;
using Il2CppProject.Code.Gameplay.Interactions.Shelfs;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace GameBridge.Commands
{
    // Thin wrappers over DevShared. Every one of these writes networked state, which only the host
    // may do, so they refuse up front instead of half-working on a client.
    internal static class CheatCommands
    {
        public static void Register(Dispatcher d)
        {
            d.Register("progress", Host(Progress));
            d.Register("box_catalog", Host(_ => OrderCheats.Catalog().Select(b => new { id = b.Id, label = b.Label }).ToList()));
            d.Register("spawn_boxes", Host(args =>
            {
                OrderCheats.Spawn((int)args["id"], args.Value<int?>("count") ?? 1);
                return new { looseBoxes = OrderCheats.LooseBoxCount() };
            }));
            d.Register("clear_boxes", Host(args =>
                args["id"] == null ? OrderCheats.ClearAll() : OrderCheats.ClearOfType((int)args["id"])));
            d.Register("time", Host(TimeCommand));
            d.Register("time_scale", args =>
            {
                if (args["scale"] != null) Time.timeScale = Mathf.Clamp((float)args["scale"], 0f, 20f);
                return new { unityTimeScale = Time.timeScale };
            });
            d.Register("employee", Host(Employee));
            d.Register("empty_shelf", Host(EmptyShelf));
            d.Register("buyers", Host(Buyers));
        }

        private static Func<JObject, object> Host(Func<JObject, object> command) => args =>
        {
            if (!GameAccess.InGame) throw new InvalidOperationException("No store is loaded.");
            if (!GameAccess.IsServer) throw new InvalidOperationException("Only the host can change the game.");
            return command(args);
        };

        private static object Progress(JObject args)
        {
            var name = (string)args["parameter"];
            if (string.IsNullOrEmpty(name))
                return Enum.GetNames(typeof(ParameterType)).ToDictionary(n => n,
                    n => (object)ProgressCheats.Get((ParameterType)Enum.Parse(typeof(ParameterType), n)));

            var type = (ParameterType)Enum.Parse(typeof(ParameterType), name, true);
            if (args["set"] != null) ProgressCheats.Set(type, (float)args["set"]);
            if (args["add"] != null) ProgressCheats.Add(type, (float)args["add"]);
            return new { parameter = type.ToString(), value = ProgressCheats.Get(type) };
        }

        private static object TimeCommand(JObject args)
        {
            if (args["hour"] != null) TimeCheats.SetClock((int)args["hour"], args.Value<int?>("minute") ?? 0);
            if (args["running"] != null) TimeCheats.SetRunning((bool)args["running"]);
            if (args["multiplier"] != null) TimeCheats.SetMultiplier((float)args["multiplier"]);
            if (args.Value<bool?>("endDay") == true) TimeCheats.ForceEndDay();
            if (args.Value<bool?>("nextDay") == true) TimeCheats.NextDay();

            TimeCheats.TryGetClock(out int hour, out int minute);
            return new { clock = $"{hour:00}:{minute:00}", running = TimeCheats.IsRunning, multiplier = TimeCheats.Multiplier };
        }

        private static object Employee(JObject args)
        {
            var action = (string)args["action"];
            if (action == "spawn_cashier")
            {
                EmployeeCheats.SpawnCashier();
                return new { done = action };
            }
            if (action == "promotion_xp")
            {
                EmployeeCheats.GivePromotionExperienceToRandom();
                return new { done = action };
            }

            var all = EmployeeCheats.All();
            int index = (int)args["index"];
            if (index < 0 || index >= all.Count)
                throw new ArgumentOutOfRangeException(nameof(index), $"There are {all.Count} employees (0-{all.Count - 1}).");
            var info = all[index];
            var value = args["value"];

            switch (action)
            {
                case "level": EmployeeCheats.SetLevel(info, (int)value); break;
                case "experience": EmployeeCheats.SetExperience(info, (float)value); break;
                case "tier": EmployeeCheats.SetTier(info, (int)value); break;
                case "promote": EmployeeCheats.Promote(info); break;
                case "boost": EmployeeCheats.Boost(info); break;
                case "clear_debt": EmployeeCheats.ClearDebt(info); break;
                case "hire": EmployeeCheats.Hire(info, value == null ? 0 : (int)value); break;
                case "profession":
                    EmployeeCheats.SetProfession(info, (EmployeeType)Enum.Parse(typeof(EmployeeType), (string)value, true));
                    break;
                default:
                    throw new ArgumentException($"Unknown employee action '{action}'.");
            }
            return JsonValues.Snapshot(info);
        }

        private static object EmptyShelf(JObject args)
        {
            var shelf = (ShelfProducts)JsonValues.Cast(Handles.Get((string)args["shelf"]), typeof(ShelfProducts));
            var mode = (string)args["mode"] ?? "delivery";
            int moved = mode == "delete" ? ShelfCheats.Delete(shelf) : ShelfCheats.ReturnToDelivery(shelf);
            return new { mode, items = moved };
        }

        private static object Buyers(JObject args)
        {
            if (args["enabled"] != null) TestingCheats.SetBuyersEnabled((bool)args["enabled"]);
            if (args.Value<bool?>("removeAll") == true) TestingCheats.RemoveAllBuyers();
            if (args["spawn"] != null)
                TestingCheats.SpawnBuyer((EBuyerType)Enum.Parse(typeof(EBuyerType), (string)args["spawn"], true));
            if (args.Value<bool?>("clean") == true) TestingCheats.ClearAllDirt();
            return new
            {
                enabled = TestingCheats.BuyersEnabled,
                count = TestingCheats.BuyerCount,
                buyerTypes = Enum.GetNames(typeof(EBuyerType)),
            };
        }
    }
}

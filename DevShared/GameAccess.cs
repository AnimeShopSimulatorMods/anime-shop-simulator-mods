using System;
using Il2CppProject.Code.Core.Services;
using Il2CppProject.Code.Core.UI;
using Il2CppProject.Code.Gameplay.Configs;
using Il2CppProject.Code.Gameplay.Controllers;
using MelonLoader;
using UnityEngine;

namespace AnimeShopMods.Dev
{
    // Every lookup the cheats need, in one place, with two routes to each object:
    // the game's service locator first, and a scene search as the fallback. Il2CppInterop cannot always
    // call a generic method like AllServices.Get<T>(), so the fallback is not optional.
    internal static class GameAccess
    {
        private static bool _serviceLocatorBroken;

        private static ParametersController _parameters;
        private static TimeController _time;
        private static OrderController _orders;
        private static EmployeesController _employees;
        private static BuyersController _buyers;
        private static ProductsController _productsController;
        private static ConfigsService _configs;
        private static UIService _ui;

        public static void Reset()
        {
            _parameters = null;
            _time = null;
            _orders = null;
            _employees = null;
            _buyers = null;
            _productsController = null;
            _configs = null;
            _ui = null;
        }

        public static ParametersController Parameters => Cached(ref _parameters);
        public static TimeController Time => Cached(ref _time);
        public static OrderController Orders => Cached(ref _orders);
        public static EmployeesController Employees => Cached(ref _employees);
        public static BuyersController Buyers => Cached(ref _buyers);

        // Named apart from Products, which is the ScriptableObject config rather than the scene object.
        public static ProductsController Products_Controller => Cached(ref _productsController);

        public static ConfigsService Configs
        {
            get
            {
                if (_configs == null) _configs = Service<ConfigsService>();
                return _configs;
            }
        }

        public static UIService Ui
        {
            get
            {
                if (_ui == null) _ui = Service<UIService>();
                return _ui;
            }
        }

        public static ProductsConfig Products
        {
            get
            {
                var configs = Configs;
                return configs == null ? null : configs.Products;
            }
        }

        public static TimeConfig TimeConfig
        {
            get
            {
                var configs = Configs;
                if (configs != null && configs.Time != null) return configs.Time;
                var controller = Time;
                return controller == null ? null : controller._timeConfig;
            }
        }

        // Nearly every cheat writes networked state, which only the server may do.
        public static bool IsServer
        {
            get
            {
                try
                {
                    var controller = Time;
                    return controller != null && controller.IsServerInitialized;
                }
                catch
                {
                    return false;
                }
            }
        }

        public static bool InGame => Time != null;

        public static T Service<T>() where T : Il2CppSystem.Object
        {
            if (_serviceLocatorBroken) return null;
            try
            {
                return AllServices.Get<T>();
            }
            catch (Exception ex)
            {
                _serviceLocatorBroken = true;
                MelonLogger.Warning($"[CheatForDev] AllServices.Get<{typeof(T).Name}>() is unusable here ({ex.GetType().Name}); " +
                                    "falling back to scene lookups.");
                return null;
            }
        }

        private static T Cached<T>(ref T slot) where T : Component
        {
            if (slot != null) return slot;
            try
            {
                slot = UnityEngine.Object.FindObjectOfType<T>();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[CheatForDev] Could not find {typeof(T).Name}: {ex.Message}");
            }
            return slot;
        }
    }
}

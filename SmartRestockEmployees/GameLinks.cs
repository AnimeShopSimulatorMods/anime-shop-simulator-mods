using System;
using Il2CppProject.Code.Core.Services;
using Il2CppProject.Code.Core.UI;
using Il2CppProject.Code.Gameplay.Configs;
using Il2CppProject.Code.Gameplay.Controllers;
using MelonLoader;
using UnityEngine;

namespace SmartRestockEmployees
{
    // The handful of game objects the shelf panel needs. Service locator first, scene search second:
    // Il2CppInterop cannot always call a generic like AllServices.Get<T>(), so the fallback is not
    // optional.
    internal static class GameLinks
    {
        private static bool _locatorBroken;
        private static OrderController _orders;
        private static TimeController _time;
        private static ConfigsService _configs;
        private static UIService _ui;

        public static void Reset()
        {
            _orders = null;
            _time = null;
            _configs = null;
            _ui = null;
        }

        public static OrderController Orders => Cached(ref _orders);
        public static TimeController Time => Cached(ref _time);

        public static ProductsConfig Products
        {
            get
            {
                var configs = Configs;
                return configs == null ? null : configs.Products;
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

        public static bool InGame => Time != null;

        // Everything the panel changes is networked state, which only the server may write.
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

        private static ConfigsService Configs
        {
            get
            {
                if (_configs == null) _configs = Service<ConfigsService>();
                return _configs;
            }
        }

        private static T Service<T>() where T : Il2CppSystem.Object
        {
            if (_locatorBroken) return null;
            try
            {
                return AllServices.Get<T>();
            }
            catch (Exception ex)
            {
                _locatorBroken = true;
                MelonLogger.Warning($"[SmartRestock] AllServices.Get<{typeof(T).Name}>() is unusable here " +
                                    $"({ex.GetType().Name}); falling back to scene lookups.");
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
                MelonLogger.Error($"[SmartRestock] Could not find {typeof(T).Name}: {ex.Message}");
            }
            return slot;
        }
    }
}

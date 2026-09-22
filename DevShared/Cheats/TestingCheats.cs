using System;
using Il2CppProject.Code.Gameplay.AI.Buyer;
using Il2CppProject.Code.Gameplay.Controllers;
using Il2CppProject.Code.Gameplay.Player.Controllers;
using MelonLoader;
using UnityEngine;

namespace AnimeShopMods.Dev.Cheats
{
    // Helpers aimed at testing the other mods in this solution rather than at playing the game:
    // stop customers interfering, fabricate the situations that are otherwise rare, move around quickly.
    internal static class TestingCheats
    {
        public static bool BuyersEnabled = true;

        public static int BuyerCount
        {
            get
            {
                try
                {
                    var controller = GameAccess.Buyers;
                    return controller == null ? 0 : controller.Count;
                }
                catch
                {
                    return 0;
                }
            }
        }

        public static void SetBuyersEnabled(bool enabled)
        {
            if (!Guard()) return;
            try
            {
                GameAccess.Buyers.DebugToggle(enabled);
                BuyersEnabled = enabled;
                DevLog.Log($"[CheatForDev] Customers {(enabled ? "enabled" : "disabled")}.");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[CheatForDev] Toggling customers failed: {ex}");
            }
        }

        public static void SpawnBuyer(EBuyerType type)
        {
            if (!Guard()) return;
            try
            {
                GameAccess.Buyers.SpawnBuyerFromDebug(type, false);
                DevLog.Log($"[CheatForDev] Spawned a {type} customer.");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[CheatForDev] Spawning a {type} customer failed: {ex}");
            }
        }

        public static void RemoveAllBuyers()
        {
            if (!Guard()) return;
            try
            {
                GameAccess.Buyers.RemoveAllBuyersAsync();
                DevLog.Log("[CheatForDev] Removing every customer.");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[CheatForDev] Removing customers failed: {ex}");
            }
        }

        public static float Cleanliness
        {
            get
            {
                try
                {
                    var dirt = UnityEngine.Object.FindObjectOfType<DirtController>();
                    return dirt == null ? 1f : dirt.Cleanliness;
                }
                catch
                {
                    return 1f;
                }
            }
        }

        public static void ClearAllDirt()
        {
            if (!GameAccess.IsServer)
            {
                MelonLogger.Warning("[CheatForDev] Only the host can clean the shop.");
                return;
            }
            try
            {
                var dirt = UnityEngine.Object.FindObjectOfType<DirtController>();
                if (dirt == null) return;
                int removed = dirt.ClearAllDirt();
                DevLog.Log($"[CheatForDev] Removed {removed} piece(s) of dirt.");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[CheatForDev] Cleaning the shop failed: {ex}");
            }
        }

        public static Transform LocalPlayer()
        {
            try
            {
                foreach (var player in UnityEngine.Object.FindObjectsOfType<PlayerCharacterController>())
                {
                    if (player == null) continue;
                    if (player.IsOwner) return player.transform;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[CheatForDev] Finding the local player failed: {ex.Message}");
            }
            return null;
        }

        public static bool TeleportTo(Vector3 position)
        {
            var player = LocalPlayer();
            if (player == null)
            {
                MelonLogger.Warning("[CheatForDev] No local player to teleport.");
                return false;
            }
            try
            {
                player.position = position;
                DevLog.Log($"[CheatForDev] Teleported to {position}.");
                return true;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[CheatForDev] Teleporting failed: {ex}");
                return false;
            }
        }

        public static bool TeleportToDelivery()
        {
            var orders = GameAccess.Orders;
            if (orders == null) return false;
            try
            {
                if (!orders.TryGetRecoveryPose(out var position, out _, false)) return false;
                return TeleportTo(position + Vector3.up * 0.1f);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[CheatForDev] Teleporting to the delivery point failed: {ex}");
                return false;
            }
        }

        private static bool Guard()
        {
            if (GameAccess.Buyers == null) return false;
            if (GameAccess.IsServer) return true;
            MelonLogger.Warning("[CheatForDev] Only the host can change customers.");
            return false;
        }
    }
}

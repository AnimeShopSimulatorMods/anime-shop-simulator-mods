using System;
using System.Collections.Generic;
using Il2CppProject.Code.Gameplay.Configs;
using Il2CppProject.Code.Gameplay.Controllers;
using MelonLoader;

namespace AnimeShopMods.Dev.Cheats
{
    // Per-employee level, experience and tier. The game exposes proper server calls for promoting and
    // hiring, so those are used where they exist; the raw fields are only written when nothing else will do.
    internal static class EmployeeCheats
    {
        public static List<EmployeeInfo> All()
        {
            var result = new List<EmployeeInfo>();
            try
            {
                var controller = GameAccess.Employees;
                if (controller == null) return result;

                var infos = controller.EmployeeInfos;
                if (infos == null) return result;

                for (int i = 0; i < infos.Count; i++)
                {
                    var info = infos[i];
                    if (info != null) result.Add(info);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[CheatForDev] Reading the employee list failed: {ex.Message}");
            }
            return result;
        }

        public static void SetLevel(EmployeeInfo info, int level)
        {
            if (!Guard() || info == null) return;
            try
            {
                info.Level = Math.Max(0, level);
                DevLog.Log($"[CheatForDev] Employee {info.Index} level set to {info.Level}.");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[CheatForDev] Setting the employee level failed: {ex}");
            }
        }

        public static void SetExperience(EmployeeInfo info, float experience)
        {
            if (!Guard() || info == null) return;
            try
            {
                info.Experience = Math.Max(0f, experience);
                DevLog.Log($"[CheatForDev] Employee {info.Index} experience set to {info.Experience}.");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[CheatForDev] Setting the employee experience failed: {ex}");
            }
        }

        public static void SetTier(EmployeeInfo info, int tier)
        {
            if (!Guard() || info == null) return;
            try
            {
                info.TierIndex = Math.Max(0, tier);
                DevLog.Log($"[CheatForDev] Employee {info.Index} tier set to {info.TierIndex}.");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[CheatForDev] Setting the employee tier failed: {ex}");
            }
        }

        public static void Promote(EmployeeInfo info)
        {
            if (!Guard() || info == null) return;
            try
            {
                GameAccess.Employees.PromoteEmployeeServer(info.Index);
                DevLog.Log($"[CheatForDev] Employee {info.Index} promoted.");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[CheatForDev] Promoting the employee failed: {ex}");
            }
        }

        public static void Boost(EmployeeInfo info)
        {
            if (!Guard() || info == null) return;
            try
            {
                GameAccess.Employees.BoostEmployeeServer(info.Index);
                DevLog.Log($"[CheatForDev] Employee {info.Index} boosted.");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[CheatForDev] Boosting the employee failed: {ex}");
            }
        }

        public static void ClearDebt(EmployeeInfo info)
        {
            if (!Guard() || info == null) return;
            try
            {
                GameAccess.Employees.PayEmployeeBillServer(info.Index);
                DevLog.Log($"[CheatForDev] Employee {info.Index} debt cleared.");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[CheatForDev] Clearing the employee debt failed: {ex}");
            }
        }

        public static void Hire(EmployeeInfo info, int tierIndex)
        {
            if (!Guard() || info == null) return;
            try
            {
                GameAccess.Employees.HireEmployeeServer(info.Index, Math.Max(0, tierIndex));
                DevLog.Log($"[CheatForDev] Employee {info.Index} hired at tier {tierIndex}.");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[CheatForDev] Hiring the employee failed: {ex}");
            }
        }

        public static void SetProfession(EmployeeInfo info, EmployeeType type)
        {
            if (!Guard() || info == null) return;
            try
            {
                GameAccess.Employees.EmployeeChangedServer(info.Index, true, type);
                DevLog.Log($"[CheatForDev] Employee {info.Index} set to {type}.");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[CheatForDev] Changing the employee profession failed: {ex}");
            }
        }

        public static void GivePromotionExperienceToRandom()
        {
            if (!Guard()) return;
            try
            {
                GameAccess.Employees.AddPromotionExperienceToRandomEmployeeServer();
                DevLog.Log("[CheatForDev] Promotion experience granted to a random employee.");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[CheatForDev] Granting promotion experience failed: {ex}");
            }
        }

        public static void SpawnCashier()
        {
            if (!Guard()) return;
            try
            {
                GameAccess.Employees.SpawnCashier();
                DevLog.Log("[CheatForDev] Cashier spawned.");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[CheatForDev] Spawning a cashier failed: {ex}");
            }
        }

        private static bool Guard()
        {
            if (GameAccess.Employees == null) return false;
            if (GameAccess.IsServer) return true;
            MelonLogger.Warning("[CheatForDev] Only the host can change employees.");
            return false;
        }
    }
}

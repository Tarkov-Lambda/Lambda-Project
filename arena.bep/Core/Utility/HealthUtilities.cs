using System;
using System.Linq;
using System.Threading.Tasks;
using Comfort.Common;
using EFT;
using EFT.UI;
using HarmonyLib;
using UnityEngine;

namespace ifp.arena.bep.Core
{
    public static class HealthUtilities
    {
        // Applies a permanent painkiller at the start of the raid
        public static void ApplyPainkiller()
        {
            var healthController = H.MainPlayer.ActiveHealthController;
            Type painkillerType = AccessTools.TypeByName("EFT.HealthSystem.ActiveHealthController+PainKiller");

            bool hasPainkiller = healthController.GetAllEffects()
                .Any(effect => effect.GetType() == painkillerType && effect.BodyPart == EBodyPart.Head);

            if (!hasPainkiller)
            {
                healthController.DoPainKiller();
            }
        }

        // This is really stupid and the amount of replenish shit im doing is really bad
        public static async Task FixMe()
        {
            var health = H.MainPlayer.ActiveHealthController;
            RU.Replenish(H.MainPlayer, true);

            health.ChangeHydration(100f);
            health.ChangeEnergy(100f);
            health.RestoreFullHealth();

            await Task.Delay(500);

            foreach (EBodyPart bodyPart in Enum.GetValues(typeof(EBodyPart)))
            {
                health.RemoveNegativeEffects(bodyPart);
            }

            health.RestoreFullHealth();
        }
    }
}

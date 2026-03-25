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
            var aHealth = H.MainPlayer.ActiveHealthController;
            Type painkillerType = AccessTools.TypeByName("EFT.HealthSystem.ActiveHealthController+PainKiller");

            bool hasPainkiller = aHealth.GetAllEffects()
                .Any(effect => effect.GetType() == painkillerType && effect.BodyPart == EBodyPart.Head);

            if (!hasPainkiller)
            {
                aHealth.DoPainKiller();
            }
        }

        // This is really stupid and the amount of replenish shit im doing is really bad
        public static async Task FixMe()
        {
            var aHealth = H.MainPlayer.ActiveHealthController;
            RU.Replenish(H.MainPlayer, true);

            aHealth.ChangeHydration(100f);
            aHealth.ChangeEnergy(100f);
            aHealth.RestoreFullHealth();

            await Task.Delay(500);

            foreach (EBodyPart bodyPart in Enum.GetValues(typeof(EBodyPart)))
            {
                aHealth.RemoveNegativeEffects(bodyPart);
            }

            aHealth.RestoreFullHealth();
        }
    }
}

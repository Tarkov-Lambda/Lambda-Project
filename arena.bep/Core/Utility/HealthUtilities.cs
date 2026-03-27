using System;
using System.Linq;
using System.Threading.Tasks;
using Comfort.Common;
using EFT;
using EFT.UI;
using Fika.Core.Main.ObservedClasses;
using Fika.Core.Main.Utils;
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

        public static void ResetObservedPlayersHealth()
        {
            if (!FikaBackendUtils.IsServer) return;

            foreach (var player in H.AllPlayers)
            {
                if (player.IsYourPlayer) continue;
                if (player.HealthController is not ObservedHealthController observedHC) continue;

                foreach (var kvp in observedHC.Dictionary_0)
                {
                    kvp.Value.Health.Current = kvp.Value.Health.Maximum;
                }
            }
        }

        // This is really stupid and the amount of replenish shit im doing is really bad
        public static async Task HealMe()
        {
            var healthController = H.MainPlayer.ActiveHealthController;
            RU.Replenish(H.MainPlayer, true);

            healthController.ChangeHydration(100f);
            healthController.ChangeEnergy(100f);
            healthController.RestoreFullHealth();

            await Task.Delay(500);

            foreach (EBodyPart bodyPart in Enum.GetValues(typeof(EBodyPart)))
            {
                healthController.RemoveNegativeEffects(bodyPart);
            }

            healthController.RestoreFullHealth();
        }
    }
}

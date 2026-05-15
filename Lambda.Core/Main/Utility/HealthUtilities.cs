using System;
using System.Linq;
using Comfort.Common;
using Cysharp.Threading.Tasks;
using Fika.Core.Main.ObservedClasses;
using Fika.Core.Main.Players;
using HarmonyLib;

namespace Lambda.Core.Main;

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
        if (!H.IsServer) return;

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

    public static void ResetObservedPlayerHealth(this ObservedPlayer player)
    {
        if (player.HealthController is not ObservedHealthController observedHC) return;

        foreach (var kvp in observedHC.Dictionary_0)
        {
            kvp.Value.Health.Current = kvp.Value.Health.Maximum;
        }
    }

    public static async UniTask HealMe()
    {
        if (H.Gamemode is IGMRespawnable)
        {
            Singleton<ReplenishPacketWarden>.Instance.Send();
        }

        var healthController = H.MainPlayer.ActiveHealthController;
        healthController.ChangeHydration(100f);
        healthController.ChangeEnergy(100f);
        healthController.RestoreFullHealth();

        await UniTask.Delay(500);

        foreach (EBodyPart bodyPart in Enum.GetValues(typeof(EBodyPart)))
        {
            healthController.RemoveNegativeEffects(bodyPart);
        }

        healthController.RestoreFullHealth();
    }
}

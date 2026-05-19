using Comfort.Common;
using Cysharp.Threading.Tasks;
using EFT;
using EFT.HealthSystem;
using Fika.Core;
using Fika.Core.Main.Utils;
using HarmonyLib;
using Lambda.Core.Main.Dying;
using Lambda.Core.Networking;
using SPT.Reflection.Patching;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Lambda.Core.Patches.Tarkov;

public class Patch_ActiveHealthController_ApplyDamage : ModulePatch
{
    public static DamageInfoStruct LastReceivedDamageInfo { get; private set; }

    public static bool IsLastDamageByOtherPlayer
    {
        get
        {
            if (LastReceivedDamageInfo.Damage != 0)
            {
                // ща блять заебато будет
                switch (LastReceivedDamageInfo.DamageType)
                {
                    case EDamageType.Bullet:
                        return true;
                    default:
                        return false;
                }
            }
            return false;
        }
    }

    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(ActiveHealthController), nameof(ActiveHealthController.ApplyDamage));

    [PatchPrefix]
    static bool Prefix(ref float __result, ActiveHealthController __instance, EBodyPart bodyPart, ref float damage, ref DamageInfoStruct damageInfo)
    {
        if (!H.IsInRaid()) return false; // mid raid connect protection
        if (H.Arena?.Session == null) return false;

        var playerScore = __instance.Player.GetContext();
        if (playerScore == null) return false;

        if (damageInfo.DamageType == EDamageType.Flame)
        {
            damageInfo.Damage *= 2.5f;
            damage *= 2.5f;
        }
        else if (damageInfo.DamageType == EDamageType.Fall)
        {
            // blacked out legs don't cause damage
            if (damage <= 3f) return false;

            // if somehow the player falls through the map on roundprepare
            if (H.Session.matchState == MatchState.RoundPrepare)
            {
                __instance.Player.MovementContext.ResetFlying();

                if (playerScore != null)
                {
                    Teleporter.Teleport(H.MainPlayer, H.Session.level, playerScore.Faction);
                }

                __instance.Player.MovementContext.ResetFlying();
                return false;
            }
        }

        LastReceivedDamageInfo = damageInfo;

        return true;
    }

    [PatchPostfix]
    static void Postfix(ref float __result, ActiveHealthController __instance)
    {

    }
}

public class Patch_ActiveHealthController_Kill : ModulePatch
{
    private static long _lastKillTime;
    private const int CooldownMs = 500;

    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(ActiveHealthController), nameof(ActiveHealthController.Kill));

    [PatchPrefix]
    static bool Prefix(ActiveHealthController __instance, EDamageType damageType)
    {
        if (!H.IsInRaid()) return false; // mid raid connect protection
        if (__instance.Player == null || __instance.Player.GetContext() == null) return false; // mid raid connect protection

        long now = Stopwatch.GetTimestamp();
        long elapsedMs = (now - _lastKillTime) * 1000 / Stopwatch.Frequency;

        if (elapsedMs < CooldownMs) return false;

        _lastKillTime = now;


        if (!__instance.Player.GetContext().IsAlive) return false;

        var lastDamage = Patch_ActiveHealthController_ApplyDamage.LastReceivedDamageInfo;

        if (H.IsServer || lastDamage.Player?.iPlayer?.Id == 1 || !Patch_ActiveHealthController_ApplyDamage.IsLastDamageByOtherPlayer)
        {
            D.Log($"{__instance.Player.Profile.Nickname} died");

            // killer can be null for environmental damage (fall, bleed, etc.)
            Player killer = null;
            if (lastDamage.Player?.iPlayer?.Id != null)
                killer = H.GetPlayerScore(lastDamage.Player.iPlayer.Id).player;

            Player victim = null;

            if (!H.IsHeadless)
            {
                victim = __instance.Player;

                if (!Patch_ActiveHealthController_ApplyDamage.IsLastDamageByOtherPlayer && killer == null)
                    killer = __instance.Player;
            }

            Singleton<PlayerKilledPacketWarden>.Instance.Send(lastDamage, victim, killer);

            // for the memes
            if (H.Session.matchState == MatchState.None)
            {
                UniTask.RunOnThreadPool(async () =>
                {
                    await UniTask.Delay(500);
                    H.MainPlayerScore.Spawn();
                    Teleporter.Teleport(__instance.Player, "lobby", Faction.None);
                });
            }
        }

        return false;
    }
}


public class Patch_ActiveHealthController_DoFracture : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(ActiveHealthController), nameof(ActiveHealthController.DoFracture));

    [PatchPrefix]
    static bool Prefix() => false;
}

public class Patch_ActiveHealthController_DoBleedGeneric : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(ActiveHealthController), nameof(ActiveHealthController.DoBleed), [typeof(bool), typeof(EBodyPart)]);

    [PatchPrefix]
    static bool Prefix() => false;
}

public class Patch_ActiveHealthController_DoBleed_HeavyBleeding : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(ActiveHealthController), nameof(ActiveHealthController.DoBleed), [typeof(EBodyPart)])
            .MakeGenericMethod(AccessTools.TypeByName("EFT.HealthSystem.ActiveHealthController+HeavyBleeding"));

    [PatchPrefix]
    static bool Prefix() => false;
}

public class Patch_ActiveHealthController_DoBleed_LightBleeding : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(ActiveHealthController), nameof(ActiveHealthController.DoBleed), [typeof(EBodyPart)])
            .MakeGenericMethod(AccessTools.TypeByName("EFT.HealthSystem.ActiveHealthController+LightBleeding"));

    [PatchPrefix]
    static bool Prefix() => false;
}

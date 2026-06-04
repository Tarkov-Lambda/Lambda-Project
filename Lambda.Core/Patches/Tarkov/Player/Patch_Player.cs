using EFT;
using EFT.Animations;
using EFT.EnvironmentEffect;
using EFT.InventoryLogic;
using Fika.Core.Main.Players;
using HarmonyLib;
using SPT.Reflection.Patching;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;
using static EFT.Player;
using static EFT.Player.FirearmController;

namespace Lambda.Core.Patches.Tarkov;

public class Patch_GClass2037_Start : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(GClass2037), nameof(GClass2037.Start));
    private static MethodInfo _baseMethod = AccessTools.Method(typeof(BaseAnimationOperationClass), nameof(BaseAnimationOperationClass.Start));


    [PatchPrefix]
    static bool Prefix(GClass2037 __instance, Action callback = null)
    {
        try
        {
            _baseMethod.Invoke(__instance, null);
            __instance.Player_0.ProceduralWeaponAnimation.TacticalReload = false;
            __instance.Action_0 = callback;
            __instance.Float_0 = 0f;
            __instance.Bool_0 = false;
            __instance.Float_1 = 0f;
            __instance.FirearmController_0.SetAnimatorAndProceduralValues();
            if (__instance.Weapon_0.IsUnderBarrelDeviceActive)
            {
                __instance.FirearmController_0.ToggleLauncher(callback);
            }
            __instance.method_5();
            __instance.FirearmController_0.method_64();

        }
        catch (Exception)
        {
            D.Log("Error in GClass2037.Start: This usually occurs when a player is trying to equip an item they are about to drop");
            if (__instance.Player_0.IsYourPlayer)
            {
                __instance.Player_0.UnfuckHands();
            }
        }

        return false;
    }
}


public class Patch_Player_ShotReactions : ModulePatch, IDisposable
{
    public static readonly Dictionary<Player, DamageInfoStruct> LastDamageToPlayer = new();

    private static Dictionary<int, long> _lastShotTimeDict = new();
    private const int CooldownMs = 15;

    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(Player), nameof(Player.ShotReactions));

    [PatchPostfix]
    static void Postfix(Player __instance, DamageInfoStruct shot, EBodyPart bodyPart)
    {
        if (shot.OverDamageFrom != null) return;
        LastDamageToPlayer[__instance] = shot;

        if (bodyPart is not EBodyPart.Head) return;

        if (!H.IsHeadless)
        {
            int killerId = shot.Player != null ? shot.Player.iPlayer.Id : 1;
            if (killerId != H.MainPlayer.Id) return;
        }


        long now = Stopwatch.GetTimestamp();

        if (_lastShotTimeDict.TryGetValue(__instance.PlayerId, out long lastShotTime))
        {
            long elapsedMs = (now - lastShotTime) * 1000 / Stopwatch.Frequency;
            if (elapsedMs < CooldownMs) return;
        }

        // Only update AFTER passing cooldown (or if new)
        _lastShotTimeDict[__instance.PlayerId] = now;

        bool didHitHelmet = shot.BlockedBy != null;

        AudioClip[] clips = didHitHelmet ? H.Sounds.HeadshotHelmet : H.Sounds.HeadshotFlesh;
        H.AudioHandler.PlayAtPoint(__instance.PlayerBody.PlayerBones.Head.position, clips.RandomElement(), 50, BetterAudio.AudioSourceGroupType.Collisions);
    }

    public void Dispose()
    {
        _lastShotTimeDict = null;
    }
}

public class Patch_Player_UpdateTick : ModulePatch
{
    private static float _lockTimer = 0f;
    private static EPlayerState _lastState = EPlayerState.Idle;

    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(Player), nameof(Player.UpdateTick));

    [PatchPostfix]
    public static void Postfix(Player __instance)
    {
        if (!__instance.IsYourPlayer) return;

        var currentState = __instance.CurrentManagedState?.Name ?? EPlayerState.Idle;

        bool isInteractionState = currentState is EPlayerState.Pickup or EPlayerState.Loot;

        if (isInteractionState)
        {
            if (currentState != _lastState)
            {
                _lockTimer = 0f;
                _lastState = currentState;
            }

            _lockTimer += Time.deltaTime;

            if (_lockTimer > 0.7f)
            {
                D.Notify("Picking up an unregistered item.");
                H.MainPlayer.MovementContext.ProcessStateEnter(new IdleStateClass(H.MainPlayer.MovementContext));

                _lockTimer = 0f;
            }
        }
        else
        {
            _lockTimer = 0f;
            _lastState = currentState;
        }
    }
}

public class Patch_Player_OnItemAddedOrRemoved : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(Player), nameof(Player.OnItemAddedOrRemoved));

    [PatchPostfix]
    public static void Postfix(Player __instance, Item item, ItemAddress location, bool added)
    {
        var pContext = __instance.Context;
        if (pContext == null) return;

        if (item.TemplateId == Hardcode.BOMB_BACKPACK)
        {
            pContext.ChangeBombCarryState(added);
        }
    }
}
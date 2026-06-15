using EFT;
using EFT.InventoryLogic;
using HarmonyLib;
using SPT.Reflection.Patching;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Lambda.Core.Patches.Tarkov;

internal class Patch_Player_ShotReactions : ModulePatch, IDisposable
{
    private static Dictionary<int, double> _lastShotTimeDict = new();
    private const double CooldownSeconds = 0.015;

    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(Player), nameof(Player.ShotReactions));

    [PatchPostfix]
    static void Postfix(Player __instance, DamageInfoStruct shot, EBodyPart bodyPart)
    {
        if (shot.OverDamageFrom != null) return;

        if (bodyPart != EBodyPart.Head) return;

        if (!H.IsHeadless)
        {
            int killerId = shot.Player != null ? shot.Player.iPlayer.Id : 1;
            if (killerId != H.MainPlayer.Id) return;
        }


        double now = Time.realtimeSinceStartupAsDouble;

        if (_lastShotTimeDict.TryGetValue(__instance.PlayerId, out double lastShotTime))
        {
            double elapsed = now - lastShotTime;
            if (elapsed < CooldownSeconds)
                return;
        }

        _lastShotTimeDict[__instance.PlayerId] = now;

        bool didHitHelmet = shot.BlockedBy != null;

        AudioClip[] clips = didHitHelmet ? H.Sounds.HeadshotHelmet : H.Sounds.HeadshotFlesh;
        H.AudioHandler.PlayAtPoint(__instance.PlayerBody.PlayerBones.Head.position, clips.RandomElement(), 80, BetterAudio.AudioSourceGroupType.Collisions);
    }

    public void Dispose()
    {
        _lastShotTimeDict = null;
    }
}

internal class Patch_Player_UpdateTick : ModulePatch
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

internal class Patch_Player_OnItemAddedOrRemoved : ModulePatch
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


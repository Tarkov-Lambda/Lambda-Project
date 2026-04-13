using EFT;
using HarmonyLib;
using ifp.arena.bep.networking.TimeSync;
using SPT.Reflection.Patching;
using System;
using System.Reflection;

namespace ifp.arena.bep.Patches.Tarkov;

internal class Patch_Gameworld_OnGameStarted : ModulePatch
{
    public static event Action OnGameStarted;

    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(GameWorld), nameof(GameWorld.OnGameStarted));

    [PatchPostfix]
    static void Postfix(GameWorld __instance)
    {
        if (__instance is HideoutGameWorld) return;

        NetworkTime.Reset();
        OnGameStarted?.Invoke();
    }
}

internal class Patch_Gameworld_OnDispose : ModulePatch
{
    public static event Action OnDispose;

    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(GameWorld), nameof(GameWorld.Dispose));

    [PatchPostfix]
    static void Postfix(GameWorld __instance)
    {
        if (!H.IsInRaid()) return;

        NetworkTime.Reset();
        OnDispose?.Invoke();
    }
}
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
#if DEBUG
#else
        if (__instance is HideoutGameWorld) return;
#endif

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
#if DEBUG
#else
        if (__instance is HideoutGameWorld) return;
#endif
        NetworkTime.Reset();
        OnDispose?.Invoke();
    }
}
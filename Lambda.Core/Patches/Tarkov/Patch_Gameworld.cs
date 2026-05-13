using EFT;
using HarmonyLib;
using PacketHandler.TimeSync;
using SPT.Reflection.Patching;
using System;
using System.Reflection;

namespace Lambda.Core.Patches.Tarkov;

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
        OnDispose?.Invoke();
    }
}
using EFT;
using EFT.Interactive;
using HarmonyLib;
using PacketWarden.TimeSync;
using SPT.Reflection.Patching;
using System;
using System.Linq;
using System.Reflection;

namespace Lambda.Core.Patches.Tarkov;

internal class Patch_Gameworld_OnGameStarted : ModulePatch
{
    public static event Action OnGameStarted;

    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(GameWorld), nameof(GameWorld.OnGameStarted));

    [PatchPostfix]
    static void Postfix(GameWorld __instance)
    {
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
        OnDispose?.Invoke();
    }
}

internal class Patch_Gameworld_RegisterLoot : ModulePatch
{
    protected override MethodBase GetTargetMethod() => typeof(GameWorld).GetMethod("RegisterLoot").MakeGenericMethod(typeof(LootItem));
    
    [PatchPrefix]
    static void Prefix(object loot)
    {
        if (loot is LootItem lootItem)
        {
            // D.Log(lootItem.name);
        }
    }
}
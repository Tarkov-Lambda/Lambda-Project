using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Comfort.Common;
using EFT;
using EFT.InputSystem;
using HarmonyLib;
using ifp.arena.bep.Core.Gamemode;
using SPT.Reflection.Patching;
using UnityEngine;

namespace ifp.arena.bep.Patches.Tarkov.UI;

internal class Patch_EftGamePlayerOwner_TranslateInventoryScreenInput : ModulePatch
{
    public static bool AllowOpenInventory = false;

    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(EftGamePlayerOwner), nameof(EftGamePlayerOwner.TranslateInventoryScreenInput));

    [PatchPrefix]
    static bool Prefix(EftGamePlayerOwner __instance, ECommand command, ref bool __result)
    {
        if (command == ECommand.ToggleInventory)
        {
            if (H.Session.matchState
                is MatchState.SideSwap
                or MatchState.MatchEnd
                or MatchState.Warmup
                or MatchState.WarmupEnd
                // or MatchState.None
                or MatchState.Cleanup)
            {
                AllowOpenInventory = false;
                __result = true;
                return false;
            }

            bool wasAllowed = AllowOpenInventory;
            AllowOpenInventory = false;

            if (wasAllowed && H.MainPlayerScore.IsAlive)
            {
                return true;
            }


            __result = true;
            return false;
        }

        return true;
    }
}

public class Patch_EftGamePlayerOwner_BlockScrollDuringMagSelect : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(EftGamePlayerOwner), nameof(EftGamePlayerOwner.TranslateCommand));

    [PatchPrefix]
    static bool Prefix(EftGamePlayerOwner __instance, ECommand command)
    {
        if (command is ECommand.ScrollNext or ECommand.ScrollPrevious)
        {
            if (IsReloadKeysDown())
            {
                return false;
            }
        }
        return true;
    }

    // there has to be a cleaner way to do this
    private static bool IsReloadKeysDown()
    {
        var settings = Singleton<SharedGameSettingsClass>.Instance?.Control?.Settings;
        var keyBindings = settings?.UserKeyBindings?.Value;
        if (keyBindings == null) return false;

        var reloadGroup = keyBindings.FirstOrDefault(x => x.keyName == EGameKey.ReloadWeapon);
        if (reloadGroup == null) return false;

        foreach (var variant in reloadGroup.variants)
        {
            if (variant.IsEmpty || variant.keyCode == null || variant.keyCode.Count == 0) continue;

            bool allKeysDown = true;
            foreach (var key in variant.keyCode)
            {
                if (!Input.GetKey(key))
                {
                    allKeysDown = false;
                    break;
                }
            }

            if (allKeysDown)
                return true;
        }

        return false;
    }
}
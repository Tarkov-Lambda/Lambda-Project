using EFT;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;
using ifp.arena.shared;
using EFT.Interactive;
using EFT.InventoryLogic;
using System.Linq;
using ifp.arena.bep.Core.Gamemode;
using Comfort.Common;
using ifp.arena.bep.networking;
using UnityEngine;
using ifp.arena.bep.GameTypes;
using ifp.arena.bep.Core.FX;
using ifp.arena.bep.Core;

namespace ifp.arena.bep.Patches.Tarkov;

internal class Patch_InteractionContextHelper_GetAvailableHideoutActions : ModulePatch
{
    protected override MethodBase GetTargetMethod() =>
    AccessTools.Method(typeof(InteractionContextHelper), nameof(InteractionContextHelper.GetAvailableHideoutActions), [typeof(HideoutPlayerOwner), typeof(IInteractive)]);

    [PatchPrefix]
    private static bool PatchPrefix(InteractionContextHelper __instance, ref ActionsReturnClass __result, HideoutPlayerOwner owner, IInteractive interactive)
    {
        return CustomInteractions.TryHandleInteraction(owner, interactive, ref __result);
    }
}

internal class Patch_InteractionContextHelper_GetAvailableActions : ModulePatch
{
    protected override MethodBase GetTargetMethod() =>
    AccessTools.Method(typeof(InteractionContextHelper), nameof(InteractionContextHelper.GetAvailableActions), [typeof(GamePlayerOwner), typeof(IInteractive)]);

    [PatchPrefix]
    private static bool PatchPrefix(InteractionContextHelper __instance, ref ActionsReturnClass __result, GamePlayerOwner owner, IInteractive interactive)
    {
        return CustomInteractions.TryHandleInteraction(owner, interactive, ref __result);
    }
}



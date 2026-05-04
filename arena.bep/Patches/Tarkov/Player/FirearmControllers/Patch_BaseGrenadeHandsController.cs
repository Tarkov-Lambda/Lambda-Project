using EFT;
using EFT.InventoryLogic;
using HarmonyLib;
using SPT.Reflection.Patching;
using System;
using System.Reflection;

namespace ifp.arena.bep.Patches.Tarkov;

public class Patch_BaseGrenadeHandsController_Drop : ModulePatch
{
    private const float DropAnimationSpeed = 1000f;

    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(Player.BaseGrenadeHandsController), nameof(Player.BaseGrenadeHandsController.Drop));

    [PatchPrefix]
    static bool Prefix(ref float animationSpeed, Action callback, bool fastDrop, Item nextControllerItem)
    {
        animationSpeed = DropAnimationSpeed;
        return true;
    }
}
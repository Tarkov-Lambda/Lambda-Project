using HarmonyLib;
using EFT;
using static EFT.Player;
using Comfort.Common;
using Lambda.Core.Networking;
using SPT.Reflection.Patching;
using System.Reflection;
using System;
using EFT.InventoryLogic;
using Fika.Core.Main.ClientClasses.HandsControllers;

namespace Lambda.Core.Patches.Tarkov;

internal class Patch_KnifeController_Drop : ModulePatch
{
    private const float DropAnimationSpeed = 1000f;

    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(FikaClientKnifeController), nameof(FikaClientKnifeController.Drop));

    [PatchPrefix]
    static bool Prefix(ref float animationSpeed)
    {
        animationSpeed = DropAnimationSpeed;
        return true;
    }
}

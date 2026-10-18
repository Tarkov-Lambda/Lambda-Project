using System.Reflection;
using EFT;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace Lambda.Core.Patches.Tarkov;

internal class Patch_BaseGrenadeHandsController_Spawn : ModulePatch
{
    private const float SpawnAnimationSpeed = 2f;

    protected override MethodBase GetTargetMethod() =>
        AccessTools.Method(
            typeof(Player.BaseGrenadeHandsController),
            nameof(Player.BaseGrenadeHandsController.Spawn)
        );

    [PatchPrefix]
    static void Prefix(ref float animationSpeed)
    {
        animationSpeed = SpawnAnimationSpeed;
    }
}

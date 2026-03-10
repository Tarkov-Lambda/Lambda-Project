using EFT;
using EFT.InventoryLogic;
using HarmonyLib;
using SPT.Reflection.Patching;
using System;
using System.Reflection;

namespace ifp.arena.bep.Patches.Tarkov
{
    public class Patch_GClass2963_Spawn : ModulePatch
    {
        private const float SpawnAnimationSpeed = 2f;

        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(EFT.Player.BaseGrenadeHandsController), nameof(EFT.Player.BaseGrenadeHandsController.Spawn));
        }

        [PatchPrefix]
        static void Prefix(ref float animationSpeed)
        {
            animationSpeed = SpawnAnimationSpeed;
        }
    }
}

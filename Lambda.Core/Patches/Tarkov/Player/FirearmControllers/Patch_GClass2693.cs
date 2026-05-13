using EFT;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;

namespace Lambda.Core.Patches.Tarkov;

    public class Patch_GClass2963_Spawn : ModulePatch
    {
        private const float SpawnAnimationSpeed = 2f;

        protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(Player.BaseGrenadeHandsController), nameof(Player.BaseGrenadeHandsController.Spawn));

        [PatchPrefix]
        static void Prefix(ref float animationSpeed)
        {
            animationSpeed = SpawnAnimationSpeed;
        }
    }
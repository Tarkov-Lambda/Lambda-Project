using Fika.Core.Main.Components;
using Fika.Core.Main.Players;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;

namespace ifp.arena.bep.Patches.Fika
{
    internal class Patch_FikaHealthBar_Awake : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(FikaHealthBar), "CreateHealthBar");
        }

        [PatchPrefix]
        static bool Prefix(FikaHealthBar __instance)
        {
            UnityEngine.Object.Destroy(__instance);
            return false;
        }
    }
}

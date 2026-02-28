using Fika.Core.Main.Components;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;


namespace ifp.arena.bep.Patches.Fika
{
    internal class Patch_FikaHealthBar_Create : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(FikaHealthBar), nameof(FikaHealthBar.Create));
        }

        [PatchPrefix]
        static bool Prefix(ref FikaHealthBar __result)
        {
            __result = null;
            return false;
        }
    }
}

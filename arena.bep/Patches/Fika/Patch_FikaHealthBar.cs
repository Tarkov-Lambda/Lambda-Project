using Fika.Core.Main.Components;
using Fika.Core.Main.Players;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;

namespace ifp.arena.bep.Patches
{
    internal class Patch_FikaHealthBar_Awake : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.PropertyGetter(typeof(Fika.Core.FikaConfig), nameof(Fika.Core.FikaConfig.UseNamePlates));
        }

        [PatchPrefix]
        static bool Prefix(ref bool __result)
        {
            __result = false;
            return false;
        }
    }
}

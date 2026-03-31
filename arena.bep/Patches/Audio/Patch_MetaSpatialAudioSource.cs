using Audio.SpatialSystem;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;

namespace ifp.arena.shared
{

    internal class Patch_MetaSpatialAudioSource_ManualUpdate : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(MetaSpatialAudioSource), "ManualUpdate");
        }

        [PatchPrefix]
        static bool Prefix()
        {
            return false;
        }
    }
}

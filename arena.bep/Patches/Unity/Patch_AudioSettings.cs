using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;

namespace ifp.arena.bep.Patches
{
    internal class Patch_AudioSettings_GetSpatializerPluginName : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(UnityEngine.AudioSettings), nameof(UnityEngine.AudioSettings.GetSpatializerPluginName));
        }

        [PatchPrefix]
        static bool Prefix(ref string __result)
        {
            D.Log("AOISNDIOASNDIOANDIONASIOD");
            __result = "Steam Audio Spatializer";
            return false;
        }
    }
}

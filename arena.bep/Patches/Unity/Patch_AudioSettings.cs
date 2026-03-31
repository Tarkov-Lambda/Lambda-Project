using Audio.SpatialSystem;
using Fika.Core.Main.Components;
using Fika.Core.Main.Players;
using HarmonyLib;
using ifp.arena.bep.Audio;
using SPT.Reflection.Patching;
using System.Reflection;
using Meta.XR.Audio;

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

using Audio.SpatialSystem;
using HarmonyLib;
using ifp.arena.shared;
using SPT.Reflection.Patching;
using System.Reflection;

namespace ifp.arena.bep.Patches
{
    internal class Patch_MetaSpatialAudioSource_SetActive : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(MetaSpatialAudioSource), nameof(MetaSpatialAudioSource.SetActive));

        [PatchPrefix]
        static bool Prefix(MetaSpatialAudioSource __instance, bool active)
        {
            var steamSpatializer = __instance.gameObject.GetComponent<SteamAudioSpatialAudioSource>();
            steamSpatializer.SetActive(active);
            __instance.gameObject.GetComponent<PhononDSPBridge>().enabled = active;

            __instance.enabled = false;
            
            return false;
        }
    }

    internal class Patch_MetaSpatialAudioSource_ManualUpdate : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(MetaSpatialAudioSource), nameof(MetaSpatialAudioSource.ManualUpdate));

        [PatchPrefix]
        static bool Prefix(MetaSpatialAudioSource __instance)
        {
            return false;
        }
    }

    internal class Patch_MetaSpatialAudioSource_enabled : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => AccessTools.PropertySetter(typeof(MetaSpatialAudioSource), nameof(MetaSpatialAudioSource.enabled));

        [PatchPrefix]
        static bool Prefix(MetaSpatialAudioSource __instance, bool value)
        {
            SteamAudioSpatialAudioSource steamAudioSpatial = __instance.gameObject.GetComponent<SteamAudioSpatialAudioSource>();
            if (steamAudioSpatial != null) steamAudioSpatial.enabled = value;

            return false;
        }
    }
}

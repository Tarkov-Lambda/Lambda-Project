using Audio.SpatialSystem;
using HarmonyLib;
using ifp.arena.shared;
using SPT.Reflection.Patching;
using SteamAudio;
using System.Reflection;
using UnityEngine;

namespace ifp.arena.shared
{
    // Destroy Meta XR on the Audio Source, plug in Steam Audio Spatializer
    // and override protected spatializer field in bettersource
    internal class Patch_BetterSource_Init : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(BetterSource), nameof(BetterSource.Init));

        [PatchPostfix]
        static void Postfix(BetterSource __instance)
        {
            var spatializerField = AccessTools.Field(typeof(BetterSource), "Spatializer");
            var current = spatializerField?.GetValue(__instance) as BaseSpatialAudioSource;

            if (current is not MetaSpatialAudioSource) return;

            var metaSpatial = __instance.gameObject.GetComponent<MetaSpatialAudioSource>();
            // if (metaSpatial != null) Object.DestroyImmediate(metaSpatial);
            if (metaSpatial != null) metaSpatial.enabled = false;

            var metaXRExp = __instance.gameObject.GetComponent<MetaXRAudioSourceExperimentalFeatures>();
            // if (metaXRExp != null) Object.DestroyImmediate(metaXRExp);
            if (metaXRExp != null) metaXRExp.enabled = false;


            var metaXR = __instance.gameObject.GetComponent<MetaXRAudioSource>();
            // if (metaXR != null) Object.DestroyImmediate(metaXR);
            if (metaXR != null) metaXR.enabled = false;

            if (current is SteamAudioSpatialAudioSource) return;

            var steamSpatializer = __instance.gameObject.GetOrAddComponent<SteamAudioSpatialAudioSource>();
            __instance.gameObject.GetOrAddComponent<SteamAudioSource>();
            __instance.gameObject.GetOrAddComponent<PhononDSPBridge>();
        
            spatializerField?.SetValue(__instance, steamSpatializer);
        }
    }

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
}

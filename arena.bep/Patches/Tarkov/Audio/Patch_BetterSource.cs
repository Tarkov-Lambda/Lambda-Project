using Audio.ReverbSubsystem;
using Audio.SpatialSystem;
using EFT;
using HarmonyLib;
using ifp.arena.shared;
using SPT.Reflection.Patching;
using SteamAudio;
using System.Reflection;
using UnityEngine;

namespace ifp.arena.bep.Patches
{
    // Destroy Meta XR on the Audio Source, plug in Steam Audio Spatializer
    // and override protected spatializer field in bettersource
    internal class Patch_BetterSource_Init : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(SimpleSource), nameof(SimpleSource.Init));

        [PatchPostfix]
        static void Postfix(BetterSource __instance, ref BaseSpatialAudioSource ___Spatializer)
        {
            if (___Spatializer is not MetaSpatialAudioSource) return;

            var metaSpatial = __instance.gameObject.GetComponent<MetaSpatialAudioSource>();
            // if (metaSpatial != null) Object.Destroy(metaSpatial);
            if (metaSpatial != null)
            {
                metaSpatial.enabled = false;
            }

            var metaXRExp = __instance.gameObject.GetComponent<MetaXRAudioSourceExperimentalFeatures>();
            // if (metaXRExp != null) Object.Destroy(metaXRExp);
            if (metaXRExp != null) metaXRExp.enabled = false;

            var metaXR = __instance.gameObject.GetComponent<MetaXRAudioSource>();
            // if (metaXR != null) Object.Destroy(metaXR);
            if (metaXR != null) metaXR.enabled = false;

            if (___Spatializer is SteamAudioSpatialAudioSource) return;

            var steamSpatializer = __instance.gameObject.GetOrAddComponent<SteamAudioSpatialAudioSource>();
            __instance.gameObject.GetOrAddComponent<SteamAudioSource>();
            __instance.gameObject.GetOrAddComponent<PhononDSPBridge>();

            ___Spatializer = steamSpatializer;
        }
    }

    internal class Patch_BetterSource_get_Spatializer : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => AccessTools.PropertyGetter(typeof(BetterSource), "Spatializer");

        [PatchPrefix]
        static bool Prefix(BetterSource __instance, ref BaseSpatialAudioSource __result, ref BaseSpatialAudioSource ___Spatializer)
        {
            if (__result is not MetaSpatialAudioSource) return true;

            var metaSpatial = __instance.gameObject.GetComponent<MetaSpatialAudioSource>();
            if (metaSpatial != null) metaSpatial.enabled = false;

            var metaXRExp = __instance.gameObject.GetComponent<MetaXRAudioSourceExperimentalFeatures>();
            if (metaXRExp != null) metaXRExp.enabled = false;

            var metaXR = __instance.gameObject.GetComponent<MetaXRAudioSource>();
            if (metaXR != null) metaXR.enabled = false;

            var steamSpatializer = __instance.gameObject.GetOrAddComponent<SteamAudioSpatialAudioSource>();
            __instance.gameObject.GetOrAddComponent<SteamAudioSource>();
            __instance.gameObject.GetOrAddComponent<PhononDSPBridge>();
            ___Spatializer = steamSpatializer;
            __result = steamSpatializer;

            return false;
        }
    }

    internal class Patch_BetterSource_UpdateSourceVolume : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(BetterSource), nameof(BetterSource.UpdateSourceVolume));

        [PatchPrefix]
        public static bool Prefix(BetterSource __instance, float speed)
        {
            // If this source has our bridge, we handle occlusion in DSP.
            // We only want Unity to handle BaseVolume (Distance) and FadeFactor.
            if (SteamSourceDict.cache.TryGetValue(__instance.source1, out var data))
            {
                // Calculate volume WITHOUT OcclusionVolumeFactor
                float targetVolume = __instance.BaseVolume * __instance.FadeFactor;

                // Apply smoothly to the source
                __instance.source1.volume = Mathf.Lerp(__instance.source1.volume, targetVolume, speed * Time.deltaTime);

                return false; // Skip original method
            }
            return true;
        }
    }
}

using HarmonyLib;
using UnityEngine;

namespace PhononSpatializerProxy.BepInEx.Patches
{
    [HarmonyPatch(typeof(AudioSource), nameof(AudioSource.spatialBlend), MethodType.Setter)]
    internal class Patch_AudioSource_set_spatialBlend
    {
        [HarmonyPrefix]
        public static bool Prefix(AudioSource __instance, ref float value)
        {
            if (AudioSourceStateBypass.Bypass) return true;

            if (SteamAudioSourceController.cache.TryGetValue(__instance, out var box))
            {
                box.Value.proxy.spatialBlend = Mathf.Clamp01(value);
                
                // If we are actively spatializing via Steam Audio, spoof Unity to 0.
                if (box.Value.proxy.spatialize)
                {
                    value = 0f; 
                }
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(AudioSource), nameof(AudioSource.spatialize), MethodType.Setter)]
    internal class Patch_AudioSource_set_spatialize
    {
        [HarmonyPrefix]
        public static bool Prefix(AudioSource __instance, ref bool value)
        {
            if (AudioSourceStateBypass.Bypass) return true;

            if (SteamAudioSourceController.cache.TryGetValue(__instance, out var box))
            {
                box.Value.proxy.spatialize = value;
                
                if (value)
                {
                    // Steam Audio is taking over. Turn off Unity's spatializer and spatial blend.
                    value = false;
                    
                    AudioSourceStateBypass.Bypass = true;
                    __instance.spatialBlend = 0f;
                    AudioSourceStateBypass.Bypass = false;
                }
                else
                {
                    // Steam Audio is OFF. Let Unity handle spatialization (which is false here anyway)
                    // But we MUST restore Unity's spatialBlend to allow native 3D amplitude panning.
                    AudioSourceStateBypass.Bypass = true;
                    __instance.spatialBlend = box.Value.proxy.spatialBlend;
                    AudioSourceStateBypass.Bypass = false;
                }
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(AudioSource), nameof(AudioSource.spatialBlend), MethodType.Getter)]
    internal class Patch_AudioSource_get_spatialBlend
    {
        [HarmonyPrefix]
        public static bool Prefix(AudioSource __instance, ref float __result)
        {
            if (AudioSourceStateBypass.Bypass) return true;

            if (SteamAudioSourceController.cache.TryGetValue(__instance, out var box))
            {
                __result = box.Value.proxy.spatialBlend;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(AudioSource), nameof(AudioSource.spatialize), MethodType.Getter)]
    internal class Patch_AudioSource_get_spatialize
    {
        [HarmonyPrefix]
        public static bool Prefix(AudioSource __instance, ref bool __result)
        {
            if (AudioSourceStateBypass.Bypass) return true;

            if (SteamAudioSourceController.cache.TryGetValue(__instance, out var box))
            {
                __result = box.Value.proxy.spatialize;
                return false;
            }
            return true;
        }
    }
}
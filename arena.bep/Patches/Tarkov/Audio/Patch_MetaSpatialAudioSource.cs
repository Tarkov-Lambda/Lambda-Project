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

        [PatchPostfix]
        static void Postfix(MetaSpatialAudioSource __instance, bool active)
        {
            // Lazy initialize the Budget Manager onto BetterAudio so it persists
            if (SteamAudioBudgetManager.Instance == null)
            {
                if (BetterAudio.Instance != null && BetterAudio.Instance.gameObject != null)
                {
                    BetterAudio.Instance.gameObject.AddComponent<SteamAudioBudgetManager>();
                }
                else
                {
                    return;
                }
            }

            if (active)
            {
                // Add to our tracked pool to be sorted by the budget manager
                SteamAudioBudgetManager.Instance.RegisterSource(__instance);
            }
            else
            {
                // Remove from the pool and immediately free the RAM
                SteamAudioBudgetManager.Instance.UnregisterSource(__instance);
            }
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

using Audio.SpatialSystem;
using HarmonyLib;
using SPT.Reflection.Patching;
using SteamAudio;
using System.Reflection;

namespace ifp.arena.bep.Patches
{
    internal class Patch_MetaXRAudioSource_enabled : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => AccessTools.PropertySetter(typeof(MetaXRAudioSource), nameof(MetaXRAudioSource.enabled));

        [PatchPostfix]
        static void Postfix(MetaXRAudioSource __instance, ref bool __result)
        {
            SteamAudioSource steamAudio = __instance.gameObject.GetComponent<SteamAudioSource>();
            if (steamAudio != null) steamAudio.enabled = __result;
        }
    }
}

using Audio.SpatialSystem;
using HarmonyLib;
using ifp.arena.shared;
using SPT.Reflection.Patching;
using System.Reflection;

namespace ifp.arena.bep.Patches;

// attach steam listener on player
internal class Patch_MetaSpatialAudioSourcem_Awake : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(MetaSpatialAudioSource), nameof(MetaSpatialAudioSource.Awake));

    [PatchPostfix]
    static void Postfix()
    {
        
    }
}
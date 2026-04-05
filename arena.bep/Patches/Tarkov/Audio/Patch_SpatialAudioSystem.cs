using Audio.ReverbSubsystem;
using Audio.SpatialSystem;
using EFT;
using HarmonyLib;
using ifp.arena.shared;
using SPT.Reflection.Patching;
using SteamAudio;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ifp.arena.bep.Patches
{
    // Destroy Meta XR on the Audio Source, plug in Steam Audio Spatializer
    // and override protected spatializer field in bettersource
    internal class Patch_SpatialAudioSystem_method_29 : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(SpatialAudioSystem), nameof(SpatialAudioSystem.method_29));

        [PatchPrefix]
        static bool Prefix(SpatialAudioSystem __instance, Dictionary<int, SourceContainerClass> ___dictionary_3, List<SourceContainerClass> ___list_0)
        {
            foreach (KeyValuePair<int, SourceContainerClass> keyValuePair in ___dictionary_3)
            {
                __instance.method_30(keyValuePair.Value);
            }
            foreach (SourceContainerClass sourceContainerClass3 in ___list_0)
            {
                __instance.method_30(sourceContainerClass3);
            }
            return false;
        }
    }
}

using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;
using Audio.ReverbSubsystem;
using UnityEngine;
using EFT;
using Audio.SpatialSystem;

namespace ifp.arena.bep.Patches;

internal class Patch_SpatialAudioSystem_Update : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(SpatialAudioSystem), nameof(SpatialAudioSystem.Update));

    [PatchPrefix]
    static bool Prefix() => false;
}


internal class Patch_SpatialAudioSystem_LateUpdate : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(SpatialAudioSystem), nameof(SpatialAudioSystem.LateUpdate));

    [PatchPrefix]
    static bool Prefix() => false;
}


// internal class Patch_InDiffEnv : ModulePatch
// {
//     protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(SpatialAudioSystem), nameof(SpatialAudioSystem.IsSourceAndListenerInDiffEnvironment));

//     [PatchPrefix]
//     static bool Prefix(ISpatialAudioRoom sourceRoom, ref bool __result)
//     {
//         var listenerRoom = CustomEnvManager.GetRoomAtPosition(MonoBehaviourSingleton<SpatialAudioSystem>.Instance.Transform_0.position);
        
//         // if one is outdoor and the other is indoor, return true
//         __result = listenerRoom.IsOutdoor != sourceRoom.IsOutdoor;
//         return false;
//     }
// }

// internal class Patch_GetListenerRoom : ModulePatch
// {
//     protected override MethodBase GetTargetMethod() => AccessTools.PropertyGetter(typeof(SpatialAudioSystem), nameof(SpatialAudioSystem.ListenerCurrentRoom));

//     [PatchPrefix]
//     static bool Prefix(ref ISpatialAudioRoom __result)
//     {
//         var listenerPos = MonoBehaviourSingleton<SpatialAudioSystem>.Instance.Transform_0.position;
//         __result = CustomEnvManager.GetRoomAtPosition(listenerPos);
//         return false; // stop Tarkov from looking at its own rooms
//     }
// }

// internal class Patch_UpdateSourceRoom : ModulePatch
// {
//     protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(SpatialAudioSystem), nameof(SpatialAudioSystem.method_30));

//     [PatchPrefix]
//     static bool Prefix(SourceContainerClass sourceContainer)
//     {
//         // force the container to use our room check based on its current position
//         sourceContainer.CurrentAudioRoom = CustomEnvManager.GetRoomAtPosition(sourceContainer.CurrentPosition);
//         return false;
//     }
// }
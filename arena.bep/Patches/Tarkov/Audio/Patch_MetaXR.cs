using HarmonyLib;
using ifp.arena.shared;
using SPT.Reflection.Patching;
using SteamAudio;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace ifp.arena.bep.Patches;

// attach steam listener on player
internal class Patch_MetaXR_EnableSpatialization : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.PropertySetter(typeof(MetaXRAudioSource), nameof(MetaXRAudioSource.EnableSpatialization));

    // [PatchPostfix]
    // static void Postfix(AudioSource ___source_, ref bool value)
    // {
    //     if (!SteamSourceDict.cache.ContainsKey(___source_)) return;

    //     SteamSourceDict.cache[___source_].bridge.spatialize = value;
    // }

    [PatchPrefix]
    public static bool Prefix(MetaXRAudioSource __instance, AudioSource ___source_, ref bool value)
    {
        // if (!SteamSourceDict.cache.ContainsKey(___source_)) return true;

        // var spatCache = SteamSourceDict.cache[___source_];

        // spatCache.bridge.spatialize = value;
        // D.Log(value.ToString());

        return true;
    }
}

internal class Patch_MetaXRAudioSource_Awake : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.PropertySetter(typeof(MetaXRAudioSource), "Awake");


    [PatchPrefix]
    public static void Postfix(MetaXRAudioSource __instance, AudioSource ___source_)
    {
        // SteamAudioSourceAttacher.
    }
}
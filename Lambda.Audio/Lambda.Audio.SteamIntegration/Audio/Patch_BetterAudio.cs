using DG.Tweening;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;

namespace Lambda.Audio.SteamIntegration.Patches;

// attach steam listener on player
internal class Patch_BetterAudio_SetProtagonist : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(BetterAudio), nameof(BetterAudio.SetProtagonist));

    [PatchPostfix]
    static void Postfix()
    {
        SteamAudioInitializer.AttachListenerIfNeeded();
    }
}

internal class Patch_BetterAudio_FadeMixerVolume : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(BetterAudio), nameof(BetterAudio.FadeMixerVolume));

    [PatchPrefix]
    static bool Prefix(BetterAudio __instance, string mixerKey, float endValDb, float seconds, bool force)
    {
        if (!__instance.Master.GetFloat(mixerKey, out var _))
        {
            // D.LogError(mixerKey + " is not found");
            return false;
        }

        DOTween.Kill(mixerKey);

        DOTween.To(
            getter: () =>
            {
                __instance.Master.GetFloat(mixerKey, out var currentValue);
                return currentValue;
            },
            setter: (x) =>
            {
                __instance.Master.SetFloat(mixerKey, x);
            },
            endValue: endValDb,
            duration: seconds
        ).SetId(mixerKey);

        return false;
    }
}
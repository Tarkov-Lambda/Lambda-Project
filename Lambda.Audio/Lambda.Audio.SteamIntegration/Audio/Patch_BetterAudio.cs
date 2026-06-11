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
        // if (SteamAudioManager.Singleton == null) return;

        // var betterAudio = Singleton<BetterAudio>.Instance;
        // if (betterAudio == null) return;

        // Transform listenerTransform = betterAudio.ListenerTransform != null ? betterAudio.ListenerTransform : betterAudio.AudioListener?.transform;

        // if (listenerTransform == null)
        // {
        //     Debug.LogError("[SteamAudio] ListenerTransform is null - SteamAudioListener not attached yet.");
        //     return;
        // }

        // listenerTransform.gameObject.GetOrAddComponent<SteamAudioListener>();
        // SteamAudioManager.NotifyAudioListenerChangedTo(listenerTransform);
        // Debug.LogError($"[SteamAudio] SteamAudioListener attached to '{listenerTransform.gameObject.name}'.");
    }
}
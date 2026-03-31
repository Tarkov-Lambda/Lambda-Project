using EFT;
using HarmonyLib;
using ifp.arena.shared;
using SPT.Reflection.Patching;
using System.Reflection;

namespace ifp.arena.shared
{
    /// <summary>
    /// Postfix on <see cref="BetterAudio.SetProtagonist"/>.
    ///
    /// <c>SetProtagonist</c> is called when the local player spawns into a raid.  By the time
    /// it returns, <c>BetterAudio.ListenerTransform</c> is populated with the
    /// <c>AudioListenerConsistencyManager</c> transform – the same object that carries Unity's
    /// <c>AudioListener</c> component.
    ///
    /// We attach <see cref="SteamAudioListener"/> here so Steam Audio can track the listener
    /// position for its simulation (HRTF orientation, reverb from listener's perspective, etc.).
    ///
    /// <see cref="SteamAudioInitializer.AttachListenerIfNeeded"/> is idempotent and safe to call
    /// multiple times (e.g. after a respawn).
    /// </summary>
    internal class Patch_BetterAudio_SetProtagonist : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
            => AccessTools.Method(typeof(BetterAudio), nameof(BetterAudio.SetProtagonist));

        [PatchPostfix]
        static void Postfix()
        {
            SteamAudioInitializer.AttachListenerIfNeeded();
        }
    }
}

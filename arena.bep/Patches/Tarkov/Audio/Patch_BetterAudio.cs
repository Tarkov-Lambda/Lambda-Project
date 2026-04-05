using HarmonyLib;
using ifp.arena.shared;
using SPT.Reflection.Patching;
using System.Reflection;

namespace ifp.arena.bep.Patches;

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
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;
using Audio.ReverbSubsystem;
using UnityEngine;
using EFT;

namespace ifp.arena.bep.Patches
{
    internal class Patch_ReverbSimpleSource_Play_Bypass : ModulePatch
    {
        private static readonly FieldInfo _trackerField = AccessTools.Field(typeof(BetterSource), "_transformTracker");
        private static FieldInfo _trackerTransformField;
        private static readonly FieldInfo _reverbSourceField = AccessTools.Field(typeof(ReverbSimpleSource), "_reverbSource");

        protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(ReverbSimpleSource), nameof(ReverbSimpleSource.Play));

        [PatchPrefix]
        public static void Prefix(ReverbSimpleSource __instance)
        {
            AudioSource internalReverb = _reverbSourceField.GetValue(__instance) as AudioSource;

            if (internalReverb == null || !SteamAudioSourceController.cache.ContainsKey(internalReverb)) return;
            var spatCache = SteamAudioSourceController.cache[internalReverb];

            if (IsLocalPlayerSource(__instance))
            {
                spatCache.bridge.IsBypass = true;
            }
            else
            {
                spatCache.bridge.IsBypass = false;

                // CRITICAL FIX FOR 2D / UNSPATIALIZED AUDIO:
                // Tarkov never sets spatialBlend=1 on _reverbSource because MetaXR ignored it.
                // We must copy the spatialization parameters from the main source so Phonon pans it in 3D!
                if (SteamAudioSourceController.cache.TryGetValue(__instance.source1, out var source1Cache))
                {
                    spatCache.bridge.spatialBlend = source1Cache.bridge.spatialBlend;
                    spatCache.bridge.spatialize = source1Cache.bridge.spatialize;
                }
            }
        }

        private static bool IsLocalPlayerSource(BetterSource source)
        {
            var mixer = source.source1.outputAudioMixerGroup;
            if (mixer == BetterAudio.Instance.ClientPlayerMovementMixer || mixer == BetterAudio.Instance.ClientPlayerSpeechMixer) return true;

            object tracker = _trackerField.GetValue(source);

            if (tracker != null)
            {
                _trackerTransformField ??= AccessTools.Field(tracker.GetType(), "Transform_0");

                Transform followTarget = (Transform)_trackerTransformField?.GetValue(tracker);

                if (followTarget != null && BetterAudio.Instance.ListenerPlayer != null)
                {
                    var localPlayerRoot = BetterAudio.Instance.ListenerPlayer.Transform.Original;
                    var localFirearm = H.MainPlayer.HandsController.WeaponRoot;

                    if (followTarget.IsChildOf(localPlayerRoot) || followTarget.IsChildOf(localFirearm))
                        return true;
                }
            }

            return false;
        }
    }
}
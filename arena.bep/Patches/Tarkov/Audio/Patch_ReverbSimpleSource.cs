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
        // Cache the fields for performance
        private static readonly FieldInfo _trackerField = AccessTools.Field(typeof(BetterSource), "_transformTracker");
        private static FieldInfo _trackerTransformField;
        private static readonly FieldInfo _reverbSourceField = AccessTools.Field(typeof(ReverbSimpleSource), "_reverbSource");

        protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(ReverbSimpleSource), nameof(ReverbSimpleSource.Play));

        [PatchPrefix]
        public static void Prefix(ReverbSimpleSource __instance)
        {
            AudioSource internalReverb = _reverbSourceField.GetValue(__instance) as AudioSource;

            if (!SteamSourceDict.cache.ContainsKey(internalReverb)) return;
            var spatCache = SteamSourceDict.cache[internalReverb];

            if (IsLocalPlayerSource(__instance))
            {
                spatCache.bridge.IsBypass = true;
                spatCache.steam.reflections = true;
            }
            else
            {
                spatCache.bridge.IsBypass = false;
                spatCache.steam.reflections = false;
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
using Audio.SpatialSystem;
using HarmonyLib;
using ifp.arena.shared;
using SPT.Reflection.Patching;
using System.Reflection;
using UnityEngine;

namespace ifp.arena.shared
{
    /// <summary>
    /// Postfix on <see cref="BetterSource.Init"/>.
    ///
    /// After Init() runs it will have found whatever <see cref="BaseSpatialAudioSource"/> was
    /// baked into the prefab (the Meta XR component) and stored it in the protected
    /// <c>Spatializer</c> field.  We swap that out for our own
    /// <see cref="SteamAudioSpatialAudioSource"/> so Steam Audio drives all further spatialization.
    ///
    /// The old component is destroyed immediately so it doesn't waste CPU in Meta XR's
    /// audio pipeline.
    /// </summary>
    internal class Patch_BetterSource_Init : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
            => AccessTools.Method(typeof(BetterSource), nameof(BetterSource.Init));

        [PatchPostfix]
        static void Postfix(BetterSource __instance)
        {
            // ── 1. Read the protected Spatializer field ────────────────────────
            var spatializerField = AccessTools.Field(typeof(BetterSource), "Spatializer");
            var current = spatializerField?.GetValue(__instance) as BaseSpatialAudioSource;

            // If it's already ours, nothing to do (re-init guard)
            if (current is SteamAudioSpatialAudioSource) return;

            // ── 2. Destroy the existing spatializer component ──────────────────
            // This removes the Meta XR component (or FakeSpatialAudioSource) from the
            // pooled audio source GameObject so it doesn't run alongside ours.
            if (current != null)
                Object.DestroyImmediate(current);

            // ── 3. Add our Steam Audio implementation ──────────────────────────
            // AddComponent fires Awake() immediately; SteamAudioSource is wired up inside
            // SteamAudioSpatialAudioSource.TryInit() which guards against a null SteamAudioManager.
            var steamSpatializer = __instance.gameObject.AddComponent<SteamAudioSpatialAudioSource>();

            // ── 4. Write the new spatializer back into the protected field ─────
            spatializerField?.SetValue(__instance, steamSpatializer);

            // NOTE: Do NOT set source1.spatialize = true here.
            // PhononDSPBridge (added inside SteamAudioSpatialAudioSource.TryInit) sets
            // spatialize = false and spatialBlend = 0 so it can handle all DSP via
            // OnAudioFilterRead.  Forcing spatialize = true here would route audio through
            // the active Unity spatializer plugin (Meta XR) instead, producing silence.
        }
    }
}

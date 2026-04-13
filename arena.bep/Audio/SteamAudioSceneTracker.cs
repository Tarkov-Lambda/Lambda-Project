using System;
using System.Collections;
using UnityEngine;

#if STEAMAUDIO_ENABLED
using SteamAudio;
#endif

namespace ifp.arena.shared;

/// <summary>
/// Tracks Steam Audio scene geometry readiness and bridges
/// <see cref="MapAssetBundleHandler"/> load/unload events to Steam Audio simulation phases.
///
/// <para>
/// Phase 1 (no geometry loaded): HRTF binaural only. Occlusion/transmission/reflections are off.
/// </para>
/// <para>
/// Phase 2 (geometry committed): Occlusion, transmission, and reflections are enabled
/// on all active <see cref="SteamAudioSpatialAudioSource"/> components.
/// </para>
///
/// Attach to the <see cref="SteamAudioManager"/> GameObject via <see cref="Register"/>.
/// </summary>
public class SteamAudioSceneTracker : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────
    //  Public state
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// True once a map scene with Steam Audio geometry has been fully loaded
    /// and the phonon scene has been committed to the simulator.
    /// </summary>
    public static bool IsSceneReady { get; private set; }

    /// <summary>
    /// Raised after a map scene finishes loading and Steam Audio geometry
    /// is committed to the simulator.  Handlers should enable Phase 2 features.
    /// </summary>
    public static event Action OnSceneReady;

    /// <summary>
    /// Raised just before a map scene is unloadeDebug.
    /// Handlers should disable Phase 2 features and return to Phase 1.
    /// </summary>
    public static event Action OnSceneCleared;

    // ─────────────────────────────────────────────────────────────────────
    //  Singleton bookkeeping
    // ─────────────────────────────────────────────────────────────────────

    private static SteamAudioSceneTracker _instance;

    /// <summary>
    /// Adds a <see cref="SteamAudioSceneTracker"/> to <paramref name="host"/> and begins
    /// listening to <see cref="MapAssetBundleHandler"/> events.  Idempotent.
    /// </summary>
    public static void Register(GameObject host)
    {
        if (host == null)
        {
            Debug.Log("[SteamAudioSceneTracker] Register() called with null host – skipping.");
            return;
        }

        if (_instance != null) return;

        foreach (var stale in host.GetComponents<SteamAudioSceneTracker>())
            UnityEngine.Object.Destroy(stale);

        _instance = host.GetOrAddComponent<SteamAudioSceneTracker>();
        Debug.Log($"[SteamAudioSceneTracker] Registered on '{host.name}'.");
    }

    // ─────────────────────────────────────────────────────────────────────
    //  MonoBehaviour lifetime
    // ─────────────────────────────────────────────────────────────────────

    private void OnEnable()
    {
        MapLoadEvent.OnSuccessfulLoad += HandleSuccessfulLoad;
        MapLoadEvent.OnBeginUnload += HandleBeginUnload;
    }

    private void OnDisable()
    {
        MapLoadEvent.OnSuccessfulLoad -= HandleSuccessfulLoad;
        MapLoadEvent.OnBeginUnload -= HandleBeginUnload;
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Event handlers
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Called when a map asset bundle scene finishes loading.
    /// We defer one frame so that:
    ///   1. All <c>SteamAudioStaticMesh</c> Awake() calls in the new scene have run and
    ///      added their geometry to the phonon scene.
    ///   2. <c>SteamAudioManager</c>'s own Update/LateUpdate has had a chance to call
    ///      <c>iplSceneCommit()</c>, making the geometry visible to the simulator's
    ///      ray caster.
    /// </summary>
    private void HandleSuccessfulLoad()
    {
        StartCoroutine(ActivateAfterFrame());
    }

    private IEnumerator ActivateAfterFrame()
    {
        // Wait for SteamAudioManager to commit the scene with the newly-loaded geometry.
        yield return null;

#if STEAMAUDIO_ENABLED
        if (SteamAudioManager.Singleton == null || SteamAudioManager.Simulator == null)
        {
            Debug.Log("[SteamAudioSceneTracker] SteamAudioManager not ready one frame after map loaDebug. " +
                         "Staying in Phase 1 (no occlusion/reflections).");
            yield break;
        }
#endif

        IsSceneReady = true;
        Debug.Log("[SteamAudioSceneTracker] Steam Audio scene geometry committed - " +
              "upgrading to Phase 2 (occlusion + transmission + reflections).");
        OnSceneReady?.Invoke();
    }

    /// <summary>
    /// Called before the map scene is unloadeDebug.  We must disable Phase 2 features
    /// before the geometry is removed from the phonon scene, otherwise in-flight
    /// ray casts can reference freed memory.
    /// </summary>
    private void HandleBeginUnload()
    {
        if (!IsSceneReady) return;

        IsSceneReady = false;
        Debug.Log("[SteamAudioSceneTracker] Map unloading - downgrading to Phase 1.");
        OnSceneCleared?.Invoke();
    }
}

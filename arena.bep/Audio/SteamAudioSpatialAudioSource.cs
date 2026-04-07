using UnityEngine;
using EFT;
using Audio.SpatialSystem;
using SteamAudio;

namespace ifp.arena.shared;
// Implementation for Spatial Audio Source for Better Source
// phase 1 is hrtf only, phase 2 is only available if the level we load has steam audio static mesh
[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(SteamAudioSource))]
[RequireComponent(typeof(PhononDSPBridge))]
public class SteamAudioSpatialAudioSource : BaseSpatialAudioSource
{
#if STEAMAUDIO_ENABLED
    private SteamAudioSource _steamSource;
    private AudioSource _audioSource;
    private PhononDSPBridge _phononDSPBridge;
#endif

    // ── cached backing fields so setters work even before SA is initialised ──

    private bool _enableSpatialization = true;
    private float _hrtfIntensity = 0.2f;
    private float _directivity = 0f;
    private bool _enableReverb = false;
    private bool _enableDirectSound = true;
    private float _reverbSendDB = -80f;
    private float _earlyReflDB = -80f;
    private float _reverbReach = 1f;
    private float _volumetricRadius = 0f;
    private bool _directSoundEnabled = true;

    /// <summary>
    /// Phase 2 reflections mix level [0, 10].  Default 0.5 keeps the reverb present without
    /// overwhelming direct sound.  Override via <see cref="ReverbSendDB"/> /
    /// <see cref="EarlyReflectionsSendDB"/> or set directly for a fixed level.
    /// </summary>
    private float _reflectionsMixLevelOverride = 0.5f;

    // ─────────────────────────────────────────────────────────────────────────
    //  Unity lifetime
    // ─────────────────────────────────────────────────────────────────────────

    public override void Awake()
    {
        base.Awake();   // sets ParentSource = GetComponent<AudioSource>()
        TryInit();

#if STEAMAUDIO_ENABLED
        SteamAudioSceneTracker.OnSceneReady += UpgradeToPhase2;
        SteamAudioSceneTracker.OnSceneCleared += DowngradeToPhase1;
#endif
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  BaseSpatialAudioSource abstract surface
    // ─────────────────────────────────────────────────────────────────────────

    public override bool EnableSpatialization
    {
        get => _enableSpatialization;
        set
        {
            _enableSpatialization = value;
            // PhononDSPBridge owns spatialize/spatialBlend – toggle its enabled flag instead
            // so HRTF is bypassed when spatialization is disabled (e.g. 2-D UI sounds).
#if STEAMAUDIO_ENABLED
            if (_phononDSPBridge != null) _phononDSPBridge.enabled = value;
#endif
        }
    }

    public override float HrtfIntensity
    {
        get => _hrtfIntensity;
        set
        {
            _hrtfIntensity = 0.2f;
#if STEAMAUDIO_ENABLED
            if (_steamSource != null) _steamSource.directMixLevel = Mathf.Clamp01(0.2f);
#endif
        }
    }

    public override float DirectivityIntensity
    {
        get => _directivity;
        set
        {
            _directivity = value;
#if STEAMAUDIO_ENABLED
            if (_steamSource != null) _steamSource.dipoleWeight = Mathf.Clamp01(value);
#endif
        }
    }

    public override bool EnableReverb
    {
        get => _enableReverb;
        set
        {
            _enableReverb = value;
#if STEAMAUDIO_ENABLED
            // Reflections are enabled/disabled by the phase system, not this flag.
            // When false, keep reflections off; when true, respect current scene phase.
            if (_steamSource != null && !value)
                _steamSource.reflections = false;
            else if (_steamSource != null && value && SteamAudioSceneTracker.IsSceneReady)
                _steamSource.reflections = true;
#endif
        }
    }

    public override bool EnableDirectSound
    {
        get => _enableDirectSound;
        set
        {
            _enableDirectSound = value;
#if STEAMAUDIO_ENABLED
            if (_steamSource != null) _steamSource.distanceAttenuation = value;
#endif
        }
    }

    /// <summary>Reverb send level in dB (-80 → 0). Mapped to Steam Audio's 0–10 reflectionsMixLevel.</summary>
    public override float ReverbSendDB
    {
        get => _reverbSendDB;
        set
        {
            _reverbSendDB = value;
#if STEAMAUDIO_ENABLED
            _reflectionsMixLevelOverride = Mathf.Clamp((value + 80f) / 80f, 0f, 10f);
            if (_steamSource != null) _steamSource.reflectionsMixLevel = _reflectionsMixLevelOverride;
#endif
        }
    }

    /// <summary>
    /// Steam Audio doesn't separate early reflections from late reverb in the same way Meta XR does;
    /// both are driven by the same reflections simulation. We store the value for completeness but
    /// map it to reflectionsMixLevel, taking the louder of the two.
    /// </summary>
    public override float EarlyReflectionsSendDB
    {
        get => _earlyReflDB;
        set
        {
            _earlyReflDB = value;
#if STEAMAUDIO_ENABLED
            // Use the stronger of reverb/early-refl to drive Steam Audio's single mix level
            _reflectionsMixLevelOverride = Mathf.Clamp((Mathf.Max(_reverbSendDB, value) + 80f) / 80f, 0f, 10f);
            if (_steamSource != null) _steamSource.reflectionsMixLevel = _reflectionsMixLevelOverride;
#endif
        }
    }

    public override float ReverbReach
    {
        get => _reverbReach;
        set
        {
            _reverbReach = value;
#if STEAMAUDIO_ENABLED
            if (_steamSource != null) _steamSource.occlusionRadius = Mathf.Max(0f, value);
#endif
        }
    }

    public override float VolumetricRadius
    {
        get => _volumetricRadius;
        set
        {
            _volumetricRadius = value;
#if STEAMAUDIO_ENABLED
            if (_steamSource != null) _steamSource.occlusionRadius = Mathf.Max(0f, value);
#endif
        }
    }

    public override bool DirectSoundEnabled
    {
        get => _directSoundEnabled;
        set
        {
            _directSoundEnabled = value;
#if STEAMAUDIO_ENABLED
            // When there is committed scene geometry, honour the flag via Steam Audio's
            // occlusion system.  Without geometry we cannot do meaningful ray casts.
            if (_steamSource != null && SteamAudioSceneTracker.IsSceneReady) _steamSource.occlusion = !value;
#endif
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Parameter application (called from Class510.method_0 / InstantiateNewSource)
    // ─────────────────────────────────────────────────────────────────────────

    public override void SetParameters(AudioGroupPreset preset)
    {
        TryInit();

#if STEAMAUDIO_ENABLED
        if (_steamSource == null) return;

        // PhononDSPBridge handles binaural – enable/disable it based on the preset.
        _enableSpatialization = preset.DirectBinaural && preset.SteamSpatialize;
        if (_phononDSPBridge != null) _phononDSPBridge.enabled = _enableSpatialization;

        // Occlusion / reflections are only meaningful once scene geometry is committed.
        // UpgradeToPhase2() / DowngradeToPhase1() handle the transitions.
        if (!SteamAudioSceneTracker.IsSceneReady)
        {
            _steamSource.occlusion = false;
            _steamSource.transmission = false;
            _steamSource.reflections = false;
            _steamSource.pathing = false;
        }
        else
        {
            ApplyPhase2Settings();
        }
#endif
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  GInterface69 members (called per-frame and on active state changes)
    // ─────────────────────────────────────────────────────────────────────────

    public override void UpdateParameters()
    {
        // SteamAudioManager drives SteamAudioSource in LateUpdate; nothing extra needed here.
    }

    public override void ManualUpdate()
    {
        // Called by BetterSource.ManualUpdate() via _updatedComps.
        // SteamAudioSource ticks itself through SteamAudioManager – no extra work required.
    }

    public override void SetActive(bool active)
    {
#if STEAMAUDIO_ENABLED
        if (_steamSource != null)
        {
            _steamSource.enabled = active;
            // _phononDSPBridge.enabled = active;
        }
#endif
    }

    private void OnDestroy()
    {
#if STEAMAUDIO_ENABLED
        SteamAudioSceneTracker.OnSceneReady -= UpgradeToPhase2;
        SteamAudioSceneTracker.OnSceneCleared -= DowngradeToPhase1;
#endif
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Internal
    // ─────────────────────────────────────────────────────────────────────────

    private void TryInit()
    {
#if STEAMAUDIO_ENABLED
        if (_steamSource != null) return;
        if (SteamAudioManager.Singleton == null) return;

        // ── SteamAudioSource: drives the simulator (occlusion rays, reflection IRs).
        // AddComponent triggers SteamAudioSource.Awake() immediately – SteamAudioManager must
        // already be up (guaranteed by SteamAudioInitializer which runs in Plugin.Start()).
        _steamSource = gameObject.GetOrAddComponent<SteamAudioSource>();
        _audioSource = gameObject.GetOrAddComponent<AudioSource>();

        // Simulation-only settings: binaural/DSP is handled by PhononDSPBridge, not
        // audioplugin_phonon, so we disable the Unity spatializer on this source.
        _steamSource.directBinaural = false;     // PhononDSPBridge does HRTF
        _steamSource.distanceAttenuation = false; // PhononDSPBridge does distance attenuation
        _steamSource.airAbsorption = false;
        _steamSource.directivity = false;
        _steamSource.directMixLevel = 1f;
        _steamSource.reflectionsMixLevel = _reflectionsMixLevelOverride;

        if (SteamAudioSceneTracker.IsSceneReady)
        {
            ApplyPhase2Settings();
        }
        else
        {
            _steamSource.occlusion = false;
            _steamSource.transmission = false;
            _steamSource.reflections = false;
            _steamSource.pathing = false;
        }

        // ── PhononDSPBridge: drives the DSP (HRTF + occlusion/transmission/reflections).
        // Its Awake() will set spatialize=false and spatialBlend=0 immediately.
        _phononDSPBridge = gameObject.GetOrAddComponent<PhononDSPBridge>();
#endif
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Phase transitions
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by <see cref="SteamAudioSceneTracker.OnSceneReady"/> when the phonon scene
    /// has been committed with geometry from the loaded map asset bundle.
    /// Enables occlusion, material-based transmission, and real-time reflections.
    /// </summary>
    private void UpgradeToPhase2()
    {
#if STEAMAUDIO_ENABLED
        if (_steamSource == null) return;

        ApplyPhase2Settings();
        Debug.Log($"[SteamAudioSpatialAudioSource] '{gameObject.name}' upgraded to Phase 2 " +
              "(occlusion + transmission + reflections).");
#endif
    }

    /// <summary>
    /// Called by <see cref="SteamAudioSceneTracker.OnSceneCleared"/> before the map scene
    /// is unloaded.  Disables simulation features that require scene geometry.
    /// </summary>
    private void DowngradeToPhase1()
    {
#if STEAMAUDIO_ENABLED
        if (_steamSource == null) return;

        _steamSource.occlusion = false;
        _steamSource.transmission = false;
        _steamSource.reflections = false;
        _steamSource.pathing = false;
        Debug.Log($"[SteamAudioSpatialAudioSource] '{gameObject.name}' downgraded to Phase 1.");
#endif
    }

    /// <summary>
    /// Applies the full Phase 2 simulation settings to the underlying <see cref="SteamAudioSource"/>.
    /// Called from both <see cref="TryInit"/> (when geometry is already available)
    /// and <see cref="UpgradeToPhase2"/>.
    /// </summary>
    private void ApplyPhase2Settings()
    {
#if STEAMAUDIO_ENABLED
        if (_steamSource == null) return;

        // ── Occlusion ─────────────────────────────────────────────────────────
        // Raycast: single shadow ray per source per sim frame – very cheap.
        // Switch to OcclusionType.Volumetric + occlusionSamples > 1 later for
        // soft transitions through doorways / thin cover.
        _steamSource.occlusion      = true;
        _steamSource.occlusionType  = OcclusionType.Raycast;
        _steamSource.occlusionSamples = 1;

        // ── Transmission ──────────────────────────────────────────────────────
        // FrequencyDependent: applies the material's per-band EQ so different wall
        // materials muffle differently (concrete vs wood, etc.)
        _steamSource.transmission         = true;
        _steamSource.transmissionType     = TransmissionType.FrequencyDependent;
        _steamSource.maxTransmissionSurfaces = 1; // penetrate at most 1 wall

        // ── Reflections ───────────────────────────────────────────────────────
        // Enable real-time convolution IR simulation so the simulator computes a
        // room IR for this source.  PhononDSPBridge reads the IR each frame via
        // SteamAudioSource.GetOutputs(SimulationFlags.Reflections) and applies:
        //   ReflectionEffect (IR convolution) → AmbisonicsDecodeEffect (HRTF binaural)
        // in its OnAudioFilterRead chain.  applyHRTFToReflections stays false so the
        // Unity audio plugin does not double-process the output.
        _steamSource.reflections            = true;
        _steamSource.reflectionsType        = ReflectionsType.Realtime;
        _steamSource.applyHRTFToReflections = false; // handled inside PhononDSPBridge
        _steamSource.reflectionsMixLevel    = Mathf.Clamp(_reflectionsMixLevelOverride, 0f, 10f);

        // ── Pathing ───────────────────────────────────────────────────────────
        // Requires pre-baked SteamAudioProbeBatch assets – leave disabled until
        // probe baking is implemented for the loaded map.
        _steamSource.pathing = false;
#endif
    }
}

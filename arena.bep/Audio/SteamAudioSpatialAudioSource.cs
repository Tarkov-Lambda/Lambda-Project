using Audio.SpatialSystem;
using UnityEngine;
using EFT;


#if DEBUG // STEAMAUDIO
using SteamAudio;
#endif

namespace ifp.arena.bep.Audio
{
    /// <summary>
    /// Implements <see cref="BaseSpatialAudioSource"/> using Steam Audio as the spatial DSP backend,
    /// replacing the Meta XR spatializer that current Tarkov builds ship with.
    ///
    /// Each pooled <see cref="BetterSource"/> gets one of these via
    /// <see cref="Patches.Tarkov.Audio.Patch_BetterSource_Init"/>, which replaces whatever
    /// BaseSpatialAudioSource was on the prefab (the Meta XR component) after BetterSource.Init() runs.
    ///
    /// Phase 1: binaural HRTF only (direct sound path). Occlusion and reflections require scene geometry
    /// tagged with SteamAudioGeometry and are left disabled until Phase 2.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class SteamAudioSpatialAudioSource : BaseSpatialAudioSource
    {
#if DEBUG // STEAMAUDIO
        private SteamAudioSource _steamSource;
#endif

        // ── cached backing fields so setters work even before SA is initialised ──

        private bool _enableSpatialization = true;
        private float _hrtfIntensity       = 1f;
        private float _directivity         = 0f;
        private bool  _enableReverb        = false;
        private bool  _enableDirectSound   = true;
        private float _reverbSendDB        = -80f;
        private float _earlyReflDB         = -80f;
        private float _reverbReach         = 1f;
        private float _volumetricRadius    = 0f;
        private bool  _directSoundEnabled  = true;

        // ─────────────────────────────────────────────────────────────────────────
        //  Unity lifetime
        // ─────────────────────────────────────────────────────────────────────────

        public override void Awake()
        {
            base.Awake();   // sets ParentSource = GetComponent<AudioSource>()
            TryInit();
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
                if (ParentSource != null)
                    ParentSource.spatialize = value;
#if DEBUG // STEAMAUDIO
                if (_steamSource != null)
                    _steamSource.directBinaural = value;
#endif
            }
        }

        public override float HrtfIntensity
        {
            get => _hrtfIntensity;
            set
            {
                _hrtfIntensity = value;
#if DEBUG // STEAMAUDIO
                if (_steamSource != null)
                    _steamSource.directMixLevel = Mathf.Clamp01(value);
#endif
            }
        }

        public override float DirectivityIntensity
        {
            get => _directivity;
            set
            {
                _directivity = value;
#if DEBUG // STEAMAUDIO
                if (_steamSource != null)
                    _steamSource.dipoleWeight = Mathf.Clamp01(value);
#endif
            }
        }

        public override bool EnableReverb
        {
            get => _enableReverb;
            set
            {
                _enableReverb = value;
#if DEBUG // STEAMAUDIO
                if (_steamSource != null)
                    _steamSource.reflections = value;
#endif
            }
        }

        public override bool EnableDirectSound
        {
            get => _enableDirectSound;
            set
            {
                _enableDirectSound = value;
#if DEBUG // STEAMAUDIO
                if (_steamSource != null)
                    _steamSource.distanceAttenuation = value;
#endif
            }
        }

        /// <summary>Reverb send level in dB (-80 → 0). Mapped to Steam Audio's 0–1 reflectionsMixLevel.</summary>
        public override float ReverbSendDB
        {
            get => _reverbSendDB;
            set
            {
                _reverbSendDB = value;
#if DEBUG // STEAMAUDIO
                if (_steamSource != null)
                    _steamSource.reflectionsMixLevel = Mathf.Clamp01((value + 80f) / 80f);
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
#if DEBUG // STEAMAUDIO
                if (_steamSource != null)
                {
                    // Use the stronger of reverb/early-refl to drive Steam Audio's single mix level
                    float combined = Mathf.Clamp01((Mathf.Max(_reverbSendDB, value) + 80f) / 80f);
                    _steamSource.reflectionsMixLevel = combined;
                }
#endif
            }
        }

        public override float ReverbReach
        {
            get => _reverbReach;
            set
            {
                _reverbReach = value;
#if DEBUG // STEAMAUDIO
                if (_steamSource != null)
                    _steamSource.occlusionRadius = Mathf.Max(0f, value);
#endif
            }
        }

        public override float VolumetricRadius
        {
            get => _volumetricRadius;
            set
            {
                _volumetricRadius = value;
#if DEBUG // STEAMAUDIO
                if (_steamSource != null)
                    _steamSource.occlusionRadius = Mathf.Max(0f, value);
#endif
            }
        }

        public override bool DirectSoundEnabled
        {
            get => _directSoundEnabled;
            set
            {
                _directSoundEnabled = value;
#if DEBUG // STEAMAUDIO
                // In Steam Audio, disabling direct sound means enabling occlusion that blocks it fully.
                // Keeping this simple: just track the value; full occlusion is Phase 2.
                if (_steamSource != null)
                    _steamSource.occlusion = !value;
#endif
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        //  Parameter application (called from Class510.method_0 / InstantiateNewSource)
        // ─────────────────────────────────────────────────────────────────────────

        public override void SetParameters(AudioGroupPreset preset)
        {
            TryInit();

#if DEBUG // STEAMAUDIO
            if (_steamSource == null) return;

            // Binaural switch comes straight from the preset
            _steamSource.directBinaural = preset.DirectBinaural;
            _enableSpatialization       = preset.DirectBinaural && preset.SteamSpatialize;

            if (ParentSource != null)
                ParentSource.spatialize = _enableSpatialization;

            // Phase 1: leave occlusion/reflections/pathing disabled until geometry is present
            _steamSource.occlusion    = false;
            _steamSource.transmission = false;
            _steamSource.reflections  = false;
            _steamSource.pathing      = false;
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
#if DEBUG // STEAMAUDIO
            if (_steamSource != null)
                _steamSource.enabled = active;
#endif
        }

        // ─────────────────────────────────────────────────────────────────────────
        //  Internal
        // ─────────────────────────────────────────────────────────────────────────

        private void TryInit()
        {
#if DEBUG // STEAMAUDIO
            if (_steamSource != null) return;
            if (SteamAudioManager.Singleton == null) return;

            // AddComponent triggers SteamAudioSource.Awake() immediately – SteamAudioManager must
            // already be up (guaranteed by SteamAudioInitializer which runs in Plugin.Start()).
            _steamSource = gameObject.GetOrAddComponent<SteamAudioSource>();

            // Phase 1 defaults: clean binaural HRTF, no occlusion, no reflections.
            _steamSource.directBinaural       = true;
            _steamSource.distanceAttenuation  = false;  // Tarkov controls rolloff via its own curves
            _steamSource.airAbsorption        = false;
            _steamSource.directivity          = false;
            _steamSource.occlusion            = false;
            _steamSource.transmission         = false;
            _steamSource.reflections          = false;
            _steamSource.pathing              = false;
            _steamSource.directMixLevel       = 1f;
            _steamSource.reflectionsMixLevel  = 1f;

            if (ParentSource != null)
                ParentSource.spatialize = _enableSpatialization;
#endif
        }
    }
}

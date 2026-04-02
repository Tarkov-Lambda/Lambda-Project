using System;
using System.Runtime.InteropServices;
using UnityEngine;
using SteamAudio;

namespace ifp.arena.shared
{
    /// <summary>
    /// Bypasses Meta XR / Unity's built-in spatializer by driving phonon.dll's
    /// BinauralEffect and DirectEffect directly from the audio thread via OnAudioFilterRead.
    ///
    /// Context and HRTF are borrowed from <see cref="SteamAudioManager"/> — no duplicate
    /// iplContextCreate / iplHRTFCreate calls. Per-source occlusion and transmission
    /// values are read from the companion <see cref="SteamAudioSource"/> component, which
    /// is ticked by <see cref="SteamAudioManager"/> each frame.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class PhononDSPBridge : MonoBehaviour
    {
        // ── P/Invokes: only per-instance effects not exposed by the managed SA API ──
        // Context / HRTF lifecycle is fully owned by SteamAudioManager.

        private const string PHONON = "phonon";

        [DllImport(PHONON, CallingConvention = CallingConvention.Cdecl)]
        static extern int iplBinauralEffectCreate(IntPtr ctx, ref PAudioSettings audio, ref PBinauralEffectSettings s, out IntPtr effect);
        [DllImport(PHONON, CallingConvention = CallingConvention.Cdecl)]
        static extern int iplBinauralEffectApply(IntPtr effect, ref PBinauralEffectParams p, ref PAudioBuffer inBuf, ref PAudioBuffer outBuf);
        [DllImport(PHONON, CallingConvention = CallingConvention.Cdecl)]
        static extern void iplBinauralEffectRelease(ref IntPtr effect);

        [DllImport(PHONON, CallingConvention = CallingConvention.Cdecl)]
        static extern int iplDirectEffectCreate(IntPtr ctx, ref PAudioSettings audio, ref PDirectEffectSettings s, out IntPtr effect);
        [DllImport(PHONON, CallingConvention = CallingConvention.Cdecl)]
        static extern int iplDirectEffectApply(IntPtr effect, ref PDirectEffectParams p, ref PAudioBuffer inBuf, ref PAudioBuffer outBuf);
        [DllImport(PHONON, CallingConvention = CallingConvention.Cdecl)]
        static extern void iplDirectEffectRelease(ref IntPtr effect);

        // ── Per-instance DSP effects ──────────────────────────────────────────

        private IntPtr _binaural = IntPtr.Zero;
        private IntPtr _direct   = IntPtr.Zero;

        // ── Unity components ──────────────────────────────────────────────────

        private AudioSource      _src;
#if STEAMAUDIO_ENABLED
        private SteamAudioSource _steamSrc;
#endif

        // ── Audio-thread parameter cache (written on main thread, read on audio thread) ──

        private PVec3  _dir       = new PVec3 { z = 1f };
        private float  _distAtten = 1f;
        private float  _occlusion = 1f;
        private float  _transLow  = 0f;
        private float  _transMid  = 0f;
        private float  _transHigh = 0f;
        private readonly object _lock = new object();

        // ── Pre-allocated DSP scratch buffers (avoid per-callback allocations) ──

        private float[] _monoIn;
        private float[] _monoOut;
        private float[] _leftOut;
        private float[] _rightOut;

        // ── Spatial blend ─────────────────────────────────────────────────────

        /// <summary>
        /// Shadow copy of the AudioSource's intended spatialBlend.
        /// The real <c>AudioSource.spatialBlend</c> is forced to 0 so neither Unity's
        /// built-in panner nor Meta XR processes the signal. This value drives Phonon's
        /// <c>IPLBinauralEffectParams.spatialBlend</c> (0 = 2-D passthrough, 1 = full HRTF)
        /// and scales distance attenuation (0 = flat, 1 = full rolloff).
        /// </summary>
        [Header("Spatial Blend")]
        [Range(0f, 1f)]
        [Tooltip("Mirrors AudioSource.spatialBlend without triggering Meta XR. " +
                 "0 = fully 2-D (no HRTF, no distance rolloff), 1 = fully 3-D.")]
        public float spatialBlendOverride = 1f;

        // ── Debug ─────────────────────────────────────────────────────────────

        [Header("Debug")]
        [Tooltip("Enable verbose log output. Disable in production.")]
        public bool verboseLogging = false;

        private string _instanceId;

        private void LogV(string msg) { if (verboseLogging) Debug.Log($"[PhononDSPBridge:{_instanceId}] {msg}"); }
        private void LogW(string msg) { Debug.LogWarning($"[PhononDSPBridge:{_instanceId}] {msg}"); }
        private void LogE(string msg) { Debug.LogError($"[PhononDSPBridge:{_instanceId}] {msg}"); }

        // ─────────────────────────────────────────────────────────────────────
        //  Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            _instanceId = $"{gameObject.name}_{GetInstanceID()}";

            _src = GetComponent<AudioSource>();
            if (_src == null) { LogE("No AudioSource found!"); return; }

#if STEAMAUDIO_ENABLED
            _steamSrc = GetComponent<SteamAudioSource>();
#endif

            // Capture the source's intended blend before disabling Unity/Meta XR spatialization.
            spatialBlendOverride  = _src.spatialBlend;
            _src.spatialize       = false;
            _src.spatialBlend     = 0f;

            // Allocate scratch buffers sized to 2× the DSP frame so a resize is never needed.
            UnityEngine.AudioSettings.GetDSPBufferSize(out int bufSize, out _);
            int cap  = bufSize * 2;
            _monoIn  = new float[cap];
            _monoOut = new float[cap];
            _leftOut = new float[cap];
            _rightOut = new float[cap];

            InitEffects();
            LogV($"Awake complete — blend={spatialBlendOverride:F2}, bufCap={cap}");
        }

        private void OnDestroy()
        {
            lock (_lock)
            {
                if (_binaural != IntPtr.Zero) { iplBinauralEffectRelease(ref _binaural); _binaural = IntPtr.Zero; }
                if (_direct   != IntPtr.Zero) { iplDirectEffectRelease(ref _direct);     _direct   = IntPtr.Zero; }
            }
            LogV("Effects released.");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Effect initialisation — borrows context + HRTF from SteamAudioManager
        // ─────────────────────────────────────────────────────────────────────

        private void InitEffects()
        {
            if (SteamAudioManager.Singleton == null || SteamAudioManager.Context == null)
            {
                LogE("SteamAudioManager not ready — cannot create effects.");
                return;
            }

            IntPtr ctx  = SteamAudioManager.Context.Get();
            IntPtr hrtf = SteamAudioManager.CurrentHRTF?.Get() ?? IntPtr.Zero;

            if (ctx == IntPtr.Zero || hrtf == IntPtr.Zero)
            {
                LogE($"Context or HRTF ptr is Zero (ctx={ctx}, hrtf={hrtf}) — cannot create effects.");
                return;
            }

            int rate;
            int frameSize;
#if STEAMAUDIO_ENABLED
            // Prefer the AudioSettings that SteamAudioManager already resolved via the audio engine.
            var saSettings = SteamAudioManager.AudioSettings;
            rate      = saSettings.samplingRate;
            frameSize = saSettings.frameSize;
#else
            rate      = UnityEngine.AudioSettings.outputSampleRate;
            UnityEngine.AudioSettings.GetDSPBufferSize(out frameSize, out _);
#endif
            var audio = new PAudioSettings { samplingRate = rate, frameSize = frameSize };

            // Binaural effect
            var binS = new PBinauralEffectSettings { hrtf = hrtf };
            int r = iplBinauralEffectCreate(ctx, ref audio, ref binS, out _binaural);
            if (r != 0 || _binaural == IntPtr.Zero)
            {
                LogE($"iplBinauralEffectCreate FAILED (code={r}) — DSP will be silent.");
                return;
            }
            LogV($"Binaural effect created (0x{_binaural.ToInt64():X})");

            // Direct effect — non-fatal; occlusion/transmission fall back to passthrough if absent.
            var dirS = new PDirectEffectSettings { numChannels = 1 };
            r = iplDirectEffectCreate(ctx, ref audio, ref dirS, out _direct);
            if (r != 0 || _direct == IntPtr.Zero)
                LogW("iplDirectEffectCreate FAILED — occlusion/transmission will be bypassed.");
            else
                LogV($"Direct effect created (0x{_direct.ToInt64():X})");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Per-frame: compute listener-relative direction + read simulation outputs
        // ─────────────────────────────────────────────────────────────────────

        private void Update()
        {
            Transform listener = GetListenerTransform();
            if (listener == null) return;

            UnityEngine.Vector3 d     = listener.InverseTransformPoint(transform.position).normalized;
            float dist    = (transform.position - listener.position).magnitude;
            float maxDist = (_src != null && _src.maxDistance > 0f) ? _src.maxDistance : 50f;
            float atten   = Mathf.Clamp01(1f - dist / maxDist);

            // Occlusion and transmission come from SteamAudioSource simulation outputs.
            // SteamAudioSpatialAudioSource enables _steamSrc.transmission only in Phase 2
            // (scene geometry committed), so we don't need a separate scene-state check here.
            float occ = 1f, tLow = 0f, tMid = 0f, tHigh = 0f;
#if STEAMAUDIO_ENABLED
            if (_steamSrc != null)
            {
                occ = Mathf.Clamp01(_steamSrc.occlusionValue);
                if (_steamSrc.transmission)
                {
                    tLow  = Mathf.Clamp01(_steamSrc.transmissionLow);
                    tMid  = Mathf.Clamp01(_steamSrc.transmissionMid);
                    tHigh = Mathf.Clamp01(_steamSrc.transmissionHigh);
                }
            }
#endif

            lock (_lock)
            {
                // Phonon uses right-hand Z-forward; Unity is left-hand Z-forward → negate Z.
                _dir       = new PVec3 { x = d.x, y = d.y, z = -d.z };
                _distAtten = atten;
                _occlusion = occ;
                _transLow  = tLow;
                _transMid  = tMid;
                _transHigh = tHigh;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  OnAudioFilterRead — runs on Unity's dedicated audio thread
        // ─────────────────────────────────────────────────────────────────────

        private unsafe void OnAudioFilterRead(float[] data, int channels)
        {
            if (channels != 2 || _binaural == IntPtr.Zero) return;

            lock (_lock)
            {
                int n = data.Length / channels;

                // Grow scratch buffers only if somehow the DSP frame size changed at runtime.
                if (n > _monoIn.Length)
                {
                    LogW($"Scratch buffer resize: n={n} > cap={_monoIn.Length}");
                    _monoIn   = new float[n];
                    _monoOut  = new float[n];
                    _leftOut  = new float[n];
                    _rightOut = new float[n];
                }

                // ── 1. Downmix to mono + distance attenuation ─────────────────
                // spatialBlendOverride lerps between flat (0) and full rolloff (1).
                float blend          = Mathf.Clamp01(spatialBlendOverride);
                float effectiveAtten = Mathf.Lerp(1f, _distAtten, blend);
                for (int i = 0; i < n; i++)
                    _monoIn[i] = (data[i * channels] + data[i * channels + 1]) * 0.5f * effectiveAtten;

                fixed (float* pIn    = _monoIn,
                              pOut   = _monoOut,
                              pLeft  = _leftOut,
                              pRight = _rightOut)
                {
                    IntPtr* inPtrs  = stackalloc IntPtr[1]; inPtrs[0]  = (IntPtr)pIn;
                    IntPtr* outPtrs = stackalloc IntPtr[1]; outPtrs[0] = (IntPtr)pOut;
                    IntPtr* binPtrs = stackalloc IntPtr[2]; binPtrs[0] = (IntPtr)pLeft; binPtrs[1] = (IntPtr)pRight;

                    var inBuf  = new PAudioBuffer { numChannels = 1, numSamples = n, data = (IntPtr)inPtrs  };
                    var outBuf = new PAudioBuffer { numChannels = 1, numSamples = n, data = (IntPtr)outPtrs };
                    var binBuf = new PAudioBuffer { numChannels = 2, numSamples = n, data = (IntPtr)binPtrs };

                    // ── 2. Direct effect: occlusion + (optional) transmission ────
                    if (_direct != IntPtr.Zero)
                    {
                        // Transmission is only applied when SteamAudioSource.transmission is
                        // active. SteamAudioSpatialAudioSource enables it only in Phase 2
                        // (scene geometry committed), so no separate scene-state check is needed.
                        bool applyTransmission = false;
#if STEAMAUDIO_ENABLED
                        applyTransmission = _steamSrc != null && _steamSrc.transmission;
#endif
                        int flags = (int)DirectEffectFlags.ApplyOcclusion;
                        if (applyTransmission) flags |= (int)DirectEffectFlags.ApplyTransmission;

                        var dp = new PDirectEffectParams
                        {
                            flags               = flags,
                            transmissionType    = 1,    // FrequencyDependent
                            distanceAttenuation = 1f,   // handled by step 1
                            airAbsorptionLow    = 1f, airAbsorptionMid = 1f, airAbsorptionHigh = 1f,
                            directivity         = 1f,
                            occlusion           = _occlusion,
                            transmissionLow     = _transLow,
                            transmissionMid     = _transMid,
                            transmissionHigh    = _transHigh,
                        };
                        iplDirectEffectApply(_direct, ref dp, ref inBuf, ref outBuf);
                    }
                    else
                    {
                        for (int i = 0; i < n; i++) _monoOut[i] = _monoIn[i];
                    }

                    // ── 3. Binaural: HRTF spatialization ────────────────────────
                    // spatialBlend = 0 → dry 2-D passthrough; 1 → full HRTF.
                    var bp = new PBinauralEffectParams
                    {
                        direction     = _dir,
                        interpolation = 1,   // bilinear
                        spatialBlend  = blend,
                        hrtf          = SteamAudioManager.CurrentHRTF?.Get() ?? IntPtr.Zero,
                        peakDelays    = IntPtr.Zero,
                    };
                    iplBinauralEffectApply(_binaural, ref bp, ref outBuf, ref binBuf);
                }

                // ── 4. Write stereo binaural result back into Unity's output buffer ──
                for (int i = 0; i < n; i++)
                {
                    data[i * channels]     = _leftOut[i];
                    data[i * channels + 1] = _rightOut[i];
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Helpers
        // ─────────────────────────────────────────────────────────────────────

        private Transform GetListenerTransform()
        {
            var cam = Camera.main;
            if (cam != null) return cam.transform;
            var al = FindObjectOfType<AudioListener>();
            return al != null ? al.transform : null;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (verboseLogging)
                Debug.Log($"[PhononDSPBridge] verboseLogging enabled on '{gameObject?.name}'.");
        }
#endif
    }

    // ── Structs for effects not exposed by the SteamAudio managed API ─────────
    // (Context + HRTF structs removed — those are owned by SteamAudioManager.)

    [StructLayout(LayoutKind.Sequential)]
    internal struct PAudioSettings { public int samplingRate; public int frameSize; }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PVec3 { public float x, y, z; }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PBinauralEffectSettings { public IntPtr hrtf; }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PBinauralEffectParams
    {
        public PVec3  direction;
        public int    interpolation;
        public float  spatialBlend;
        public IntPtr hrtf;
        public IntPtr peakDelays;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PDirectEffectSettings { public int numChannels; }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PDirectEffectParams
    {
        public int   flags;
        public int   transmissionType;
        public float distanceAttenuation;
        public float airAbsorptionLow, airAbsorptionMid, airAbsorptionHigh;
        public float directivity;
        public float occlusion;
        public float transmissionLow, transmissionMid, transmissionHigh;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PAudioBuffer
    {
        public int    numChannels;
        public int    numSamples;
        public IntPtr data;
    }
}

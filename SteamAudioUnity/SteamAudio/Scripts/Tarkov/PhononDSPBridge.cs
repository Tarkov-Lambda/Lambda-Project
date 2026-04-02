using System;
using System.Runtime.InteropServices;
using UnityEngine;
using SteamAudio;
using System.Collections.Generic;

namespace ifp.arena.shared
{
    /// <summary>
    /// Bypasses Meta XR / Unity's built-in spatializer by driving phonon.dll's
    /// BinauralEffect, DirectEffect, ReflectionEffect, and AmbisonicsDecodeEffect
    /// directly from the audio thread via OnAudioFilterRead.
    ///
    /// Context and HRTF are borrowed from <see cref="SteamAudioManager"/> — no duplicate
    /// iplContextCreate / iplHRTFCreate calls. Per-source occlusion, transmission, and
    /// reflection values are read from the companion <see cref="SteamAudioSource"/>
    /// component, which is ticked by <see cref="SteamAudioManager"/> each frame.
    ///
    /// Reflection pipeline (mirrors spatialize_effect.cpp):
    ///   mono → iplReflectionEffectApply → N-ch ambisonics
    ///        → iplAmbisonicsDecodeEffectApply → stereo
    ///        → mix into binaural output
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class PhononDSPBridge : MonoBehaviour
    {
        // ── P/Invokes: per-instance effects not exposed by the managed SA API ───
        // Context / HRTF lifecycle is fully owned by SteamAudioManager.

        private const string PHONON = "phonon";

        // ── Direct + Binaural ──────────────────────────────────────────────────

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

        // ── Reflections ────────────────────────────────────────────────────────

        [DllImport(PHONON, CallingConvention = CallingConvention.Cdecl)]
        static extern int iplReflectionEffectCreate(IntPtr ctx, ref PAudioSettings audio, ref PReflectionEffectSettings s, out IntPtr effect);
        [DllImport(PHONON, CallingConvention = CallingConvention.Cdecl)]
        static extern int iplReflectionEffectApply(IntPtr effect, ref PReflectionEffectParams p, ref PAudioBuffer inBuf, ref PAudioBuffer outBuf, IntPtr mixer);
        [DllImport(PHONON, CallingConvention = CallingConvention.Cdecl)]
        static extern void iplReflectionEffectRelease(ref IntPtr effect);

        [DllImport(PHONON, CallingConvention = CallingConvention.Cdecl)]
        static extern int iplAmbisonicsDecodeEffectCreate(IntPtr ctx, ref PAudioSettings audio, ref PAmbisonicsDecodeEffectSettings s, out IntPtr effect);
        [DllImport(PHONON, CallingConvention = CallingConvention.Cdecl)]
        static extern int iplAmbisonicsDecodeEffectApply(IntPtr effect, ref PAmbisonicsDecodeEffectParams p, ref PAudioBuffer inBuf, ref PAudioBuffer outBuf);
        [DllImport(PHONON, CallingConvention = CallingConvention.Cdecl)]
        static extern void iplAmbisonicsDecodeEffectRelease(ref IntPtr effect);

        // ── Per-instance DSP effects ──────────────────────────────────────────

        private IntPtr _binaural = IntPtr.Zero;
        private IntPtr _direct = IntPtr.Zero;
        private IntPtr _reflectionEffect = IntPtr.Zero;
        private IntPtr _ambisonicsEffect = IntPtr.Zero;

        // Number of ambisonics channels = (order+1)^2, set during InitEffects.
        private int _numAmbiChannels = 4;   // default 1st-order

        // ── Unity components ──────────────────────────────────────────────────

        private AudioSource _src;
#if STEAMAUDIO_ENABLED
        private SteamAudioSource _steamSrc;
#endif

        // ── Audio-thread parameter cache (written on main thread, read on audio thread) ──

        private PVec3 _dir = new PVec3 { z = 1f };
        private float _distAtten = 1f;
        private float _occlusion = 1f;
        private float _transLow = 0f;
        private float _transMid = 0f;
        private float _transHigh = 0f;

        // Reflections cache
        private bool _reflectionsActive;
        private PReflectionEffectParams _cachedReflParams;
        private PCoordinateSpace3 _listenerCoords;
        private float _cachedReflMixLevel = 1f;
        private bool _cachedApplyHRTFToRefl = true;

        private readonly object _lock = new object();

        // ── Pre-allocated DSP scratch buffers (avoid per-callback allocations) ──

        private float[] _monoIn;
        private float[] _monoOut;
        private float[] _leftOut;
        private float[] _rightOut;

        // Reflection scratch: flat contiguous array for ambisonics channels.
        // Layout: channel 0 occupies [0..n-1], channel 1 [n..2n-1], etc.
        // This allows a single GC pin and arithmetic channel pointer construction.
        private float[] _ambiFlat;
        private float[] _reflStereoLeft;
        private float[] _reflStereoRight;

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

        // ── Reflections (inspector) ───────────────────────────────────────────

        [Header("Reflections")]
        [Tooltip("Apply HRTF (binaural decode) to the reflection ambisonics output. " +
                 "Requires SteamAudioSource.reflections = true.")]
        public bool applyHRTFToReflections = true;

        [Tooltip("Mix level applied to the decoded reflection signal before summing " +
                 "with the direct binaural output.")]
        [Range(0f, 10f)]
        public float reflectionsMixLevel = 1f;

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
            spatialBlendOverride = _src.spatialBlend;
            _src.spatialize = false;
            _src.spatialBlend = 0f;

            // Allocate scratch buffers sized to 2× the DSP frame so a resize is never needed.
            UnityEngine.AudioSettings.GetDSPBufferSize(out int bufSize, out _);
            int cap = bufSize * 2;
            _monoIn = new float[cap];
            _monoOut = new float[cap];
            _leftOut = new float[cap];
            _rightOut = new float[cap];

            // Reflection buffers are allocated in InitEffects once we know _numAmbiChannels.

            InitEffects();
            LogV($"Awake complete — blend={spatialBlendOverride:F2}, bufCap={cap}");
        }

        private void OnDestroy()
        {
            lock (_lock)
            {
                if (_binaural != IntPtr.Zero) { iplBinauralEffectRelease(ref _binaural); _binaural = IntPtr.Zero; }
                if (_direct != IntPtr.Zero) { iplDirectEffectRelease(ref _direct); _direct = IntPtr.Zero; }
                if (_reflectionEffect != IntPtr.Zero) { iplReflectionEffectRelease(ref _reflectionEffect); _reflectionEffect = IntPtr.Zero; }
                if (_ambisonicsEffect != IntPtr.Zero) { iplAmbisonicsDecodeEffectRelease(ref _ambisonicsEffect); _ambisonicsEffect = IntPtr.Zero; }
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

            IntPtr ctx = SteamAudioManager.Context.Get();
            IntPtr hrtf = SteamAudioManager.CurrentHRTF?.Get() ?? IntPtr.Zero;

            if (ctx == IntPtr.Zero || hrtf == IntPtr.Zero)
            {
                LogE($"Context or HRTF ptr is Zero (ctx={ctx}, hrtf={hrtf}) — cannot create effects.");
                return;
            }

            int rate;
            int frameSize;
#if STEAMAUDIO_ENABLED
            var saSettings = SteamAudioManager.AudioSettings;
            rate = saSettings.samplingRate;
            frameSize = saSettings.frameSize;
#else
            rate      = UnityEngine.AudioSettings.outputSampleRate;
            UnityEngine.AudioSettings.GetDSPBufferSize(out frameSize, out _);
#endif
            var audio = new PAudioSettings { samplingRate = rate, frameSize = frameSize };

            // ── Binaural effect ────────────────────────────────────────────────
            var binS = new PBinauralEffectSettings { hrtf = hrtf };
            int r = iplBinauralEffectCreate(ctx, ref audio, ref binS, out _binaural);
            if (r != 0 || _binaural == IntPtr.Zero)
            {
                LogE($"iplBinauralEffectCreate FAILED (code={r}) — DSP will be silent.");
                return;
            }
            LogV($"Binaural effect created (0x{_binaural.ToInt64():X})");

            // ── Direct effect ──────────────────────────────────────────────────
            var dirS = new PDirectEffectSettings { numChannels = 1 };
            r = iplDirectEffectCreate(ctx, ref audio, ref dirS, out _direct);
            if (r != 0 || _direct == IntPtr.Zero)
                LogW("iplDirectEffectCreate FAILED — occlusion/transmission will be bypassed.");
            else
                LogV($"Direct effect created (0x{_direct.ToInt64():X})");

            // ── Reflection + Ambisonics effects ───────────────────────────────
            // These are non-fatal: if they fail, reflections are simply skipped.
            InitReflectionEffects(ctx, hrtf, ref audio, rate);
        }

        // ── ADD THESE P/INVOKES TO PhononDSPBridge ──
        [DllImport(PHONON, CallingConvention = CallingConvention.Cdecl)]
        public static extern void iplReflectionEffectReset(IntPtr effect);

        [DllImport(PHONON, CallingConvention = CallingConvention.Cdecl)]
        public static extern void iplAmbisonicsDecodeEffectReset(IntPtr effect);

        // ── ADD THIS POOL CLASS ANYWHERE IN YOUR SCRIPT ──
        public class ReflectionSlot
        {
            public IntPtr reflectionEffect;
            public IntPtr ambisonicsEffect;
            public float[] ambiFlat;
            public float[] reflStereoLeft;
            public float[] reflStereoRight;
            public int numAmbiChannels;
        }

        public static class NativeReflectionPool
        {
            public static readonly Stack<ReflectionSlot> AvailableSlots = new Stack<ReflectionSlot>();
            public static bool IsInitialized = false;
            private static readonly object _poolLock = new object();

            public static void Initialize(int maxReflections)
            {
                if (IsInitialized) return;
                lock (_poolLock)
                {
                    if (IsInitialized) return;

                    IntPtr ctx = SteamAudioManager.Context.Get();
                    IntPtr hrtf = SteamAudioManager.CurrentHRTF?.Get() ?? IntPtr.Zero;
                    if (ctx == IntPtr.Zero || hrtf == IntPtr.Zero) return;

                    int rate = SteamAudioManager.AudioSettings.samplingRate;
                    int frameSize = SteamAudioManager.AudioSettings.frameSize;
                    var audio = new PAudioSettings { samplingRate = rate, frameSize = frameSize };

                    var ss = SteamAudioSettings.Singleton;
                    int order = ss.realTimeAmbisonicOrder;
                    int numAmbiChannels = (order + 1) * (order + 1);
                    int irSize = Mathf.CeilToInt(ss.realTimeDuration * rate);
                    if (irSize <= 0) irSize = rate;

                    int reflType = (int)SteamAudioManager.GetReflectionEffectType();
                    var reflSettings = new PReflectionEffectSettings { type = reflType, numChannels = numAmbiChannels, irSize = irSize };

                    var ambiSettings = new PAmbisonicsDecodeEffectSettings
                    {
                        speakerLayout = new PSpeakerLayout { type = 1, numSpeakers = 0, speakers = IntPtr.Zero },
                        hrtf = hrtf,
                        maxOrder = order,
                    };

                    // Pre-allocate the exact budget to prevent OOM
                    for (int i = 0; i < maxReflections; i++)
                    {
                        if (iplReflectionEffectCreate(ctx, ref audio, ref reflSettings, out IntPtr reflPtr) == 0 &&
                            iplAmbisonicsDecodeEffectCreate(ctx, ref audio, ref ambiSettings, out IntPtr ambiPtr) == 0)
                        {
                            int cap = frameSize * 2;
                            AvailableSlots.Push(new ReflectionSlot
                            {
                                reflectionEffect = reflPtr,
                                ambisonicsEffect = ambiPtr,
                                ambiFlat = new float[numAmbiChannels * cap],
                                reflStereoLeft = new float[cap],
                                reflStereoRight = new float[cap],
                                numAmbiChannels = numAmbiChannels
                            });
                        }
                    }
                    IsInitialized = true;
                }
            }

            public static ReflectionSlot Borrow()
            {
                lock (_poolLock)
                {
                    return AvailableSlots.Count > 0 ? AvailableSlots.Pop() : null;
                }
            }

            public static void Return(ReflectionSlot slot)
            {
                if (slot == null) return;
                lock (_poolLock)
                {
                    // Reset history to prevent gunshot echos bleeding into footsteps
                    if (slot.reflectionEffect != IntPtr.Zero) iplReflectionEffectReset(slot.reflectionEffect);
                    if (slot.ambisonicsEffect != IntPtr.Zero) iplAmbisonicsDecodeEffectReset(slot.ambisonicsEffect);
                    AvailableSlots.Push(slot);
                }
            }

            public static void Cleanup()
            {
                lock (_poolLock)
                {
                    while (AvailableSlots.Count > 0)
                    {
                        var slot = AvailableSlots.Pop();
                        if (slot.reflectionEffect != IntPtr.Zero) iplReflectionEffectRelease(ref slot.reflectionEffect);
                        if (slot.ambisonicsEffect != IntPtr.Zero) iplAmbisonicsDecodeEffectRelease(ref slot.ambisonicsEffect);
                    }
                    IsInitialized = false;
                }
            }
        }

        // Add this field
        private ReflectionSlot _activeSlot;

        public void AssignReflectionSlot(ReflectionSlot slot)
        {
            lock (_lock)
            {
                _activeSlot = slot;
#if STEAMAUDIO_ENABLED
                if (_steamSrc != null) _steamSrc.reflections = true;
#endif
            }
        }

        public ReflectionSlot RevokeReflectionSlot()
        {
            lock (_lock)
            {
                var slot = _activeSlot;
                _activeSlot = null;
#if STEAMAUDIO_ENABLED
                if (_steamSrc != null) _steamSrc.reflections = false;
#endif
                return slot;
            }
        }

        private void InitReflectionEffects(IntPtr ctx, IntPtr hrtf, ref PAudioSettings audio, int rate)
        {
#if STEAMAUDIO_ENABLED
            // ── Guard: only allocate convolution IR memory for sources that opt in. ──────────
            // Each IPLReflectionEffect (convolution) pre-allocates ~(numCh × irSize × 4) bytes
            // of overlap-add convolution buffers internally. Without this guard every bridge
            // instance in a Tarkov raid (100+ sources) allocates that memory unconditionally,
            // causing IPL_STATUS_OUTOFMEMORY. Only create the effect when the companion
            // SteamAudioSource has reflections explicitly enabled.
            if (_steamSrc == null || !_steamSrc.reflections)
            {
                LogV("SteamAudioSource.reflections is false — skipping reflection effect allocation.");
                return;
            }

            if (SteamAudioSettings.Singleton == null)
            {
                LogW("SteamAudioSettings not available — reflections disabled.");
                return;
            }

            var ss = SteamAudioSettings.Singleton;

            // Determine ambisonics order and channel count.
            int order = ss.realTimeAmbisonicOrder;
            _numAmbiChannels = (order + 1) * (order + 1);

            // IR size in samples.
            int irSize = Mathf.CeilToInt(ss.realTimeDuration * rate);
            if (irSize <= 0) irSize = rate; // safety fallback to 1 s

            // reflectionType: 0=Convolution, 1=Parametric, 2=Hybrid, 3=TAN
            int reflType = (int)SteamAudioManager.GetReflectionEffectType();

            var reflSettings = new PReflectionEffectSettings
            {
                type = reflType,
                numChannels = _numAmbiChannels,
                irSize = irSize,
            };

            int r = iplReflectionEffectCreate(ctx, ref audio, ref reflSettings, out _reflectionEffect);
            if (r != 0 || _reflectionEffect == IntPtr.Zero)
            {
                LogW($"iplReflectionEffectCreate FAILED (code={r}) — reflections disabled.");
                return;
            }
            LogV($"Reflection effect created (0x{_reflectionEffect.ToInt64():X}), " +
                 $"order={order}, numAmbiCh={_numAmbiChannels}, irSize={irSize}, type={reflType}");

            // Speaker layout: Stereo = type 1.
            var ambiSettings = new PAmbisonicsDecodeEffectSettings
            {
                speakerLayout = new PSpeakerLayout { type = 1, numSpeakers = 0, speakers = IntPtr.Zero },
                hrtf = hrtf,
                maxOrder = order,
            };

            r = iplAmbisonicsDecodeEffectCreate(ctx, ref audio, ref ambiSettings, out _ambisonicsEffect);
            if (r != 0 || _ambisonicsEffect == IntPtr.Zero)
            {
                LogW($"iplAmbisonicsDecodeEffectCreate FAILED (code={r}) — reflections disabled.");
                iplReflectionEffectRelease(ref _reflectionEffect);
                _reflectionEffect = IntPtr.Zero;
                return;
            }
            LogV($"Ambisonics decode effect created (0x{_ambisonicsEffect.ToInt64():X})");

            // Allocate ambisonics scratch buffers now that we know channel count.
            int cap = audio.frameSize * 2;
            _ambiFlat = new float[_numAmbiChannels * cap];
            _reflStereoLeft = new float[cap];
            _reflStereoRight = new float[cap];
            LogV($"Reflection scratch buffers allocated (ambiCap={_numAmbiChannels * cap})");
#else
            LogV("STEAMAUDIO_ENABLED not defined — reflections disabled.");
#endif
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Per-frame: compute listener-relative direction + read simulation outputs
        // ─────────────────────────────────────────────────────────────────────

        private void Update()
        {
            Transform listener = GetListenerTransform();
            if (listener == null) return;

            UnityEngine.Vector3 d = listener.InverseTransformPoint(transform.position).normalized;
            float dist = (transform.position - listener.position).magnitude;
            float maxDist = (_src != null && _src.maxDistance > 0f) ? _src.maxDistance : 50f;
            float atten = Mathf.Clamp01(1f - dist / maxDist);

            float occ = 1f, tLow = 0f, tMid = 0f, tHigh = 0f;
#if STEAMAUDIO_ENABLED
            if (_steamSrc != null)
            {
                occ = Mathf.Clamp01(_steamSrc.occlusionValue);
                if (_steamSrc.transmission)
                {
                    tLow = Mathf.Clamp01(_steamSrc.transmissionLow);
                    tMid = Mathf.Clamp01(_steamSrc.transmissionMid);
                    tHigh = Mathf.Clamp01(_steamSrc.transmissionHigh);
                }
            }
#endif

            // Compute listener coordinate space for ambisonics decode.
            // Unity → Steam Audio: negate Z component of all vectors/positions.
            var lCoords = new PCoordinateSpace3
            {
                right = new PVec3 { x = listener.right.x, y = listener.right.y, z = -listener.right.z },
                up = new PVec3 { x = listener.up.x, y = listener.up.y, z = -listener.up.z },
                ahead = new PVec3 { x = listener.forward.x, y = listener.forward.y, z = -listener.forward.z },
                origin = new PVec3 { x = listener.position.x, y = listener.position.y, z = -listener.position.z },
            };

            // Reflection params from simulation (non-blocking — returns last completed cycle's data).
            bool reflActive = false;
            PReflectionEffectParams reflParams = default;
#if STEAMAUDIO_ENABLED
            if (_steamSrc != null && _steamSrc.reflections && _reflectionEffect != IntPtr.Zero)
            {
                try
                {
                    var outputs = _steamSrc.GetOutputs(SimulationFlags.Reflections);
                    var rp = outputs.reflections;

                    // Only activate once the simulator has produced a valid IR / param set.
                    if (rp.ir != IntPtr.Zero || rp.reverbTimesLow > 0f || rp.reverbTimesMid > 0f)
                    {
                        reflActive = true;

                        int reflType = (int)SteamAudioManager.GetReflectionEffectType();
                        int irSize = SteamAudioSettings.Singleton != null
                            ? Mathf.CeilToInt(SteamAudioSettings.Singleton.realTimeDuration *
                                              (SteamAudioManager.AudioSettings.samplingRate > 0
                                                  ? SteamAudioManager.AudioSettings.samplingRate
                                                  : UnityEngine.AudioSettings.outputSampleRate))
                            : UnityEngine.AudioSettings.outputSampleRate;

                        reflParams = new PReflectionEffectParams
                        {
                            type = reflType,
                            ir = rp.ir,
                            reverbTimesLow = rp.reverbTimesLow,
                            reverbTimesMid = rp.reverbTimesMid,
                            reverbTimesHigh = rp.reverbTimesHigh,
                            eqLow = rp.eqLow,
                            eqMid = rp.eqMid,
                            eqHigh = rp.eqHigh,
                            delay = rp.delay,
                            numChannels = _numAmbiChannels,
                            irSize = irSize,
                            tanDevice = IntPtr.Zero,   // TAN not supported in bridge
                            tanSlot = 0,
                        };
                    }
                }
                catch (Exception ex)
                {
                    LogW($"GetOutputs(Reflections) threw: {ex.Message}");
                }
            }
#endif

            lock (_lock)
            {
                // Phonon uses right-hand Z-forward; Unity is left-hand Z-forward → negate Z.
                _dir = new PVec3 { x = d.x, y = d.y, z = -d.z };
                _distAtten = atten;
                _occlusion = occ;
                _transLow = tLow;
                _transMid = tMid;
                _transHigh = tHigh;

                _reflectionsActive = reflActive;
                _cachedReflParams = reflParams;
                _listenerCoords = lCoords;
                _cachedReflMixLevel = reflectionsMixLevel;
                _cachedApplyHRTFToRefl = applyHRTFToReflections;
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

                // Grow scratch buffers only if the DSP frame size changed at runtime.
                if (n > _monoIn.Length)
                {
                    LogW($"Scratch buffer resize: n={n} > cap={_monoIn.Length}");
                    _monoIn = new float[n];
                    _monoOut = new float[n];
                    _leftOut = new float[n];
                    _rightOut = new float[n];
                }

                // ── 1. Downmix to mono + distance attenuation ─────────────────
                float blend = Mathf.Clamp01(spatialBlendOverride);
                float effectiveAtten = Mathf.Lerp(1f, _distAtten, blend);
                for (int i = 0; i < n; i++)
                    _monoIn[i] = (data[i * channels] + data[i * channels + 1]) * 0.5f * effectiveAtten;

                fixed (float* pIn = _monoIn,
                              pOut = _monoOut,
                              pLeft = _leftOut,
                              pRight = _rightOut)
                {
                    IntPtr* inPtrs = stackalloc IntPtr[1]; inPtrs[0] = (IntPtr)pIn;
                    IntPtr* outPtrs = stackalloc IntPtr[1]; outPtrs[0] = (IntPtr)pOut;
                    IntPtr* binPtrs = stackalloc IntPtr[2]; binPtrs[0] = (IntPtr)pLeft; binPtrs[1] = (IntPtr)pRight;

                    var inBuf = new PAudioBuffer { numChannels = 1, numSamples = n, data = (IntPtr)inPtrs };
                    var outBuf = new PAudioBuffer { numChannels = 1, numSamples = n, data = (IntPtr)outPtrs };
                    var binBuf = new PAudioBuffer { numChannels = 2, numSamples = n, data = (IntPtr)binPtrs };

                    // ── 2. Direct effect: occlusion + (optional) transmission ────
                    if (_direct != IntPtr.Zero)
                    {
                        bool applyTransmission = false;
#if STEAMAUDIO_ENABLED
                        applyTransmission = _steamSrc != null && _steamSrc.transmission;
#endif
                        int flags = (int)DirectEffectFlags.ApplyOcclusion;
                        if (applyTransmission) flags |= (int)DirectEffectFlags.ApplyTransmission;

                        var dp = new PDirectEffectParams
                        {
                            flags = flags,
                            transmissionType = 1,    // FrequencyDependent
                            distanceAttenuation = 1f,   // handled by step 1
                            airAbsorptionLow = 1f,
                            airAbsorptionMid = 1f,
                            airAbsorptionHigh = 1f,
                            directivity = 1f,
                            occlusion = _occlusion,
                            transmissionLow = _transLow,
                            transmissionMid = _transMid,
                            transmissionHigh = _transHigh,
                        };
                        iplDirectEffectApply(_direct, ref dp, ref inBuf, ref outBuf);
                    }
                    else
                    {
                        for (int i = 0; i < n; i++) _monoOut[i] = _monoIn[i];
                    }

                    // ── 3. Binaural: HRTF spatialization ────────────────────────
                    var bp = new PBinauralEffectParams
                    {
                        direction = _dir,
                        interpolation = 1,   // bilinear
                        spatialBlend = blend,
                        hrtf = SteamAudioManager.CurrentHRTF?.Get() ?? IntPtr.Zero,
                        peakDelays = IntPtr.Zero,
                    };
                    iplBinauralEffectApply(_binaural, ref bp, ref outBuf, ref binBuf);
                }

                // ── 4. Write stereo binaural result back into Unity's output buffer ──
                for (int i = 0; i < n; i++)
                {
                    data[i * channels] = _leftOut[i];
                    data[i * channels + 1] = _rightOut[i];
                }

                // ── 5. Reflections ────────────────────────────────────────────
                // Runs after the direct binaural write so we can additive-mix on top.
                var state = _activeSlot;
                if (_reflectionsActive && state != null && state.reflectionEffect != IntPtr.Zero)
                {
                    ApplyReflections(data, channels, n, state);
                }
            }
        }

        public void ToggleReflectionDSP(bool enable)
        {
            if (_steamSrc == null) return;

            // Check if we are already in the correct state to avoid redundant calls
            if (enable == (_reflectionEffect != IntPtr.Zero)) return;

            // Tell the SteamAudioManager simulator to start/stop tracing rays for this source
            _steamSrc.reflections = enable;

            lock (_lock)
            {
                if (enable && _reflectionEffect == IntPtr.Zero)
                {
                    IntPtr ctx = SteamAudioManager.Context.Get();
                    IntPtr hrtf = SteamAudioManager.CurrentHRTF?.Get() ?? IntPtr.Zero;
                    if (ctx == IntPtr.Zero || hrtf == IntPtr.Zero) return;

                    int rate;
                    int frameSize;
#if STEAMAUDIO_ENABLED
                    var saSettings = SteamAudioManager.AudioSettings;
                    rate = saSettings.samplingRate;
                    frameSize = saSettings.frameSize;
#else
            rate = UnityEngine.AudioSettings.outputSampleRate;
            UnityEngine.AudioSettings.GetDSPBufferSize(out frameSize, out _);
#endif
                    var audio = new PAudioSettings { samplingRate = rate, frameSize = frameSize };

                    // Allocate the heavy convolution memory
                    InitReflectionEffects(ctx, hrtf, ref audio, rate);
                }
                else if (!enable && _reflectionEffect != IntPtr.Zero)
                {
                    // Free the heavy convolution memory instantly
                    iplReflectionEffectRelease(ref _reflectionEffect);
                    _reflectionEffect = IntPtr.Zero;

                    if (_ambisonicsEffect != IntPtr.Zero)
                    {
                        iplAmbisonicsDecodeEffectRelease(ref _ambisonicsEffect);
                        _ambisonicsEffect = IntPtr.Zero;
                    }
                }
            }
        }

        // Modify ApplyReflections to take the ReflectionSlot:
        private unsafe void ApplyReflections(float[] data, int channels, int n, ReflectionSlot state)
        {
            fixed (float* pMono = _monoIn,
                          pAmbi = state.ambiFlat,
                          pRSL = state.reflStereoLeft,
                          pRSR = state.reflStereoRight)
            {
                // Build mono input buffer
                IntPtr* monoPtr = stackalloc IntPtr[1]; monoPtr[0] = (IntPtr)pMono;
                var monoBuf = new PAudioBuffer { numChannels = 1, numSamples = n, data = (IntPtr)monoPtr };

                // Build ambisonics output buffer
                IntPtr* ambiPtrs = stackalloc IntPtr[state.numAmbiChannels];
                for (int c = 0; c < state.numAmbiChannels; c++)
                    ambiPtrs[c] = (IntPtr)(pAmbi + c * n);
                var ambiBuf = new PAudioBuffer { numChannels = state.numAmbiChannels, numSamples = n, data = (IntPtr)ambiPtrs };

                // Build stereo decode output buffer
                IntPtr* rsPtrs = stackalloc IntPtr[2]; rsPtrs[0] = (IntPtr)pRSL; rsPtrs[1] = (IntPtr)pRSR;
                var rsBuf = new PAudioBuffer { numChannels = 2, numSamples = n, data = (IntPtr)rsPtrs };

                // Apply Native Effects using the pre-allocated pointers
                var reflParams = _cachedReflParams;
                iplReflectionEffectApply(state.reflectionEffect, ref reflParams, ref monoBuf, ref ambiBuf, IntPtr.Zero);

                IntPtr hrtf = _cachedApplyHRTFToRefl ? (SteamAudioManager.CurrentHRTF?.Get() ?? IntPtr.Zero) : IntPtr.Zero;
                var ambiParams = new PAmbisonicsDecodeEffectParams
                {
                    order = (int)Mathf.Sqrt(state.numAmbiChannels) - 1,
                    hrtf = hrtf,
                    orientation = _listenerCoords,
                    binaural = (_cachedApplyHRTFToRefl && hrtf != IntPtr.Zero) ? 1 : 0,
                    normalizeEQ = 0,
                };
                iplAmbisonicsDecodeEffectApply(state.ambisonicsEffect, ref ambiParams, ref ambiBuf, ref rsBuf);
            }

            // Additive mix
            float ml = _cachedReflMixLevel;
            for (int i = 0; i < n; i++)
            {
                data[i * channels] += state.reflStereoLeft[i] * ml;
                data[i * channels + 1] += state.reflStereoRight[i] * ml;
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

    /// <summary>Matches IPLCoordinateSpace3 in phonon.h.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct PCoordinateSpace3
    {
        public PVec3 right;
        public PVec3 up;
        public PVec3 ahead;
        public PVec3 origin;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PBinauralEffectSettings { public IntPtr hrtf; }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PBinauralEffectParams
    {
        public PVec3 direction;
        public int interpolation;
        public float spatialBlend;
        public IntPtr hrtf;
        public IntPtr peakDelays;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PDirectEffectSettings { public int numChannels; }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PDirectEffectParams
    {
        public int flags;
        public int transmissionType;
        public float distanceAttenuation;
        public float airAbsorptionLow, airAbsorptionMid, airAbsorptionHigh;
        public float directivity;
        public float occlusion;
        public float transmissionLow, transmissionMid, transmissionHigh;
    }

    /// <summary>Matches IPLReflectionEffectSettings in phonon.h.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct PReflectionEffectSettings
    {
        public int type;         // IPLReflectionEffectType: 0=Convolution, 1=Parametric, 2=Hybrid, 3=TAN
        public int numChannels;  // (order+1)^2
        public int irSize;       // maxDuration * samplingRate
    }

    /// <summary>Matches IPLReflectionEffectParams in phonon.h.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct PReflectionEffectParams
    {
        public int type;
        public IntPtr ir;              // IPLImpulseResponse handle (null for parametric/hybrid)
        public float reverbTimesLow;
        public float reverbTimesMid;
        public float reverbTimesHigh;
        public float eqLow;
        public float eqMid;
        public float eqHigh;
        public int delay;
        public int numChannels;
        public int irSize;
        public IntPtr tanDevice;       // Always IntPtr.Zero — TAN not supported in bridge
        public int tanSlot;
    }

    /// <summary>
    /// Matches IPLSpeakerLayout in phonon.h.
    /// type: 0=Mono, 1=Stereo, 2=Quadraphonic, 3=FivePointOne, 4=SevenPointOne, 5=Custom.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct PSpeakerLayout
    {
        public int type;
        public int numSpeakers;
        public IntPtr speakers;   // IPLVector3* — null for standard layouts
    }

    /// <summary>Matches IPLAmbisonicsDecodeEffectSettings in phonon.h.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct PAmbisonicsDecodeEffectSettings
    {
        public PSpeakerLayout speakerLayout;
        public IntPtr hrtf;
        public int maxOrder;
    }

    /// <summary>Matches IPLAmbisonicsDecodeEffectParams in phonon.h.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct PAmbisonicsDecodeEffectParams
    {
        public int order;
        public IntPtr hrtf;
        public PCoordinateSpace3 orientation;
        public int binaural;    // IPLbool
        public int normalizeEQ; // IPLbool — leave 0 for default behaviour
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PAudioBuffer
    {
        public int numChannels;
        public int numSamples;
        public IntPtr data;
    }
}

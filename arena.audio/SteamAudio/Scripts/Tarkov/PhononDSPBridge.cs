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
    /// Phase 1 (geometry not committed):
    ///   Direct signal → DirectEffect (occlusion/transmission) → BinauralEffect (HRTF) → output
    ///
    /// Phase 2 (geometry committed, reflections enabled on SteamAudioSource):
    ///   Direct signal → DirectEffect → BinauralEffect → output
    ///   Raw signal    → ReflectionEffect (convolution IR) → AmbisonicsDecodeEffect (binaural) → mixed into output
    ///
    /// Context, HRTF, and Simulator are borrowed from <see cref="SteamAudioManager"/>.
    /// Simulation outputs (IR, occlusion values) are read from the companion <see cref="SteamAudioSource"/>.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class PhononDSPBridge : MonoBehaviour
    {
        // ── P/Invoke DLL name ─────────────────────────────────────────────────
        private const string PHONON = "phonon";

        // ── Binaural Effect ───────────────────────────────────────────────────
        [DllImport(PHONON, CallingConvention = CallingConvention.Cdecl)]
        static extern int iplBinauralEffectCreate(IntPtr ctx, ref PAudioSettings audio, ref PBinauralEffectSettings s, out IntPtr effect);
        [DllImport(PHONON, CallingConvention = CallingConvention.Cdecl)]
        static extern int iplBinauralEffectApply(IntPtr effect, ref PBinauralEffectParams p, ref PAudioBuffer inBuf, ref PAudioBuffer outBuf);
        [DllImport(PHONON, CallingConvention = CallingConvention.Cdecl)]
        static extern void iplBinauralEffectRelease(ref IntPtr effect);

        // ── Direct Effect (occlusion / transmission) ──────────────────────────
        [DllImport(PHONON, CallingConvention = CallingConvention.Cdecl)]
        static extern int iplDirectEffectCreate(IntPtr ctx, ref PAudioSettings audio, ref PDirectEffectSettings s, out IntPtr effect);
        [DllImport(PHONON, CallingConvention = CallingConvention.Cdecl)]
        static extern int iplDirectEffectApply(IntPtr effect, ref PDirectEffectParams p, ref PAudioBuffer inBuf, ref PAudioBuffer outBuf);
        [DllImport(PHONON, CallingConvention = CallingConvention.Cdecl)]
        static extern void iplDirectEffectRelease(ref IntPtr effect);

        // ── Reflection Effect (convolution IR) ───────────────────────────────
        [DllImport(PHONON, CallingConvention = CallingConvention.Cdecl)]
        static extern int iplReflectionEffectCreate(IntPtr ctx, ref PAudioSettings audio, ref PReflectionEffectSettings s, out IntPtr effect);
        [DllImport(PHONON, CallingConvention = CallingConvention.Cdecl)]
        static extern int iplReflectionEffectApply(IntPtr effect, ref ReflectionEffectParams p, ref PAudioBuffer inBuf, ref PAudioBuffer outBuf, IntPtr mixer);
        [DllImport(PHONON, CallingConvention = CallingConvention.Cdecl)]
        static extern void iplReflectionEffectRelease(ref IntPtr effect);

        // ── Ambisonics Decode Effect (ambi → binaural stereo) ────────────────
        [DllImport(PHONON, CallingConvention = CallingConvention.Cdecl)]
        static extern int iplAmbisonicsDecodeEffectCreate(IntPtr ctx, ref PAudioSettings audio, ref PAmbisonicsDecodeEffectSettings s, out IntPtr effect);
        [DllImport(PHONON, CallingConvention = CallingConvention.Cdecl)]
        static extern int iplAmbisonicsDecodeEffectApply(IntPtr effect, ref PAmbisonicsDecodeEffectParams p, ref PAudioBuffer inBuf, ref PAudioBuffer outBuf);
        [DllImport(PHONON, CallingConvention = CallingConvention.Cdecl)]
        static extern void iplAmbisonicsDecodeEffectRelease(ref IntPtr effect);

        // ── Native Audio Buffer (for reflection ambisonic intermediates) ──────
        [DllImport(PHONON, CallingConvention = CallingConvention.Cdecl)]
        static extern int iplAudioBufferAllocate(IntPtr ctx, int numChannels, int numSamples, out PAudioBuffer buf);
        [DllImport(PHONON, CallingConvention = CallingConvention.Cdecl)]
        static extern void iplAudioBufferFree(IntPtr ctx, ref PAudioBuffer buf);

        // ── Effect handles ────────────────────────────────────────────────────
        private IntPtr _binaural = IntPtr.Zero;
        private IntPtr _direct = IntPtr.Zero;
        private IntPtr _reflectionEffect = IntPtr.Zero;
        private IntPtr _ambiDecodeEffect = IntPtr.Zero;

        // ── Native-allocated buffers for the reflection pipeline ──────────────
        // Allocated by iplAudioBufferAllocate; data pointer is stable after allocation.
        private PAudioBuffer _reflAmbiNative;   // (maxOrder+1)^2 ambisonic channels
        private PAudioBuffer _reflStereoNative; // 2 stereo channels
        // Guard flag: written on game thread LAST (volatile → memory fence), read on audio thread.
        private volatile bool _reflBufsAllocated = false;

        // Reflection pipeline parameters (set once in InitEffects, read-only after that)
        private int _maxAmbiChannels = 4;  // (1+1)^2 for order 1
        private int _irSize = 0;
        private int _nativeFrameSize = 0;  // frame size the native buffers were allocated for
        private int _cachedMaxOrder = 1;

        // ── Component refs ────────────────────────────────────────────────────
        private AudioSource _src;
#if STEAMAUDIO_ENABLED
        private SteamAudioSource _steamSrc;
#endif

        // per-frame data (game thread -> audio thread, protected by _lock)
        private PVec3 _dir = new PVec3 { z = 1f };
        private float _distAtten = 1f;
        private float _occlusion = 1f;
        private float _transLow = 0f;
        private float _transMid = 0f;
        private float _transHigh = 0f;
        private bool _applyTransmission = false;
        private IntPtr _hrtf = IntPtr.Zero;

        // reflection per-frame cache
        private ReflectionEffectParams _cachedReflParams;
        private bool _hasValidReflData = false;
        private PCoordinateSpace3 _listenerCS;
        private float _reflMixLevel = 1f;

        private readonly object _lock = new object();

        // managed scratch buffers for the direct path
        private float[] _monoIn;
        private float[] _monoOut;
        private float[] _leftOut;
        private float[] _rightOut;

        // distance attenuation curve cache
        private AnimationCurve _cachedRolloffCurve;
        private float _lastMaxDist = -1f;

        // proxying these unity calls to this field and forcing real spatialize and spatialBlend fields to false and 0f
        // AudioSource_set_spatialize
        // AudioSource_set_spatialBlend
        // AudioSource_get_spatialBlend
        public float spatialBlend = 1f;
        public bool spatialize = true;

        [Header("Debug")]
        public bool verboseLogging = false;

        // turn off the audio source silently
        public bool IsBypass = false;

        private string _instanceId;

        private IntPtr _cachedContext = IntPtr.Zero;

#if DEBUG
        private void LogV(string msg) { if (verboseLogging) Debug.Log($"[PhononDSPBridge:{_instanceId}] {msg}"); }
#else
        private void LogV(string msg) { }
#endif
        private void LogW(string msg) { Debug.LogWarning($"[PhononDSPBridge:{_instanceId}] {msg}"); }
        private void LogE(string msg) { Debug.LogError($"[PhononDSPBridge:{_instanceId}] {msg}"); }

        private void Awake()
        {
            _instanceId = $"{gameObject.name}_{GetInstanceID()}";
            _src = GetComponent<AudioSource>();
            if (_src == null) { LogE("No AudioSource found!"); return; }

#if STEAMAUDIO_ENABLED
            _steamSrc = GetComponent<SteamAudioSource>();
#endif

            UnityEngine.AudioSettings.GetDSPBufferSize(out int bufSize, out _);
            int cap = bufSize * 2;
            _monoIn = new float[cap];
            _monoOut = new float[cap];
            _leftOut = new float[cap];
            _rightOut = new float[cap];

            // first attempt; will silently no-op if SteamAudioManager / HRTF not ready yet.
            // Update() retries every frame until everything is created.
            InitEffects();
        }

        private void OnDestroy()
        {
            // destroy is called from the game thread; the audio thread should be
            // quiesced by Unity before this point, but we lock anyway to be safe.
            lock (_lock)
            {
                if (_binaural != IntPtr.Zero) { iplBinauralEffectRelease(ref _binaural); _binaural = IntPtr.Zero; }
                if (_direct != IntPtr.Zero) { iplDirectEffectRelease(ref _direct); _direct = IntPtr.Zero; }
                if (_reflectionEffect != IntPtr.Zero) { iplReflectionEffectRelease(ref _reflectionEffect); _reflectionEffect = IntPtr.Zero; }
                if (_ambiDecodeEffect != IntPtr.Zero) { iplAmbisonicsDecodeEffectRelease(ref _ambiDecodeEffect); _ambiDecodeEffect = IntPtr.Zero; }

                if (_reflBufsAllocated)
                {
                    _reflBufsAllocated = false;

                    if (_cachedContext != IntPtr.Zero)
                    {
                        if (_reflAmbiNative.data != IntPtr.Zero) iplAudioBufferFree(_cachedContext, ref _reflAmbiNative);
                        if (_reflStereoNative.data != IntPtr.Zero) iplAudioBufferFree(_cachedContext, ref _reflStereoNative);
                    }
                }
            }
        }

        private void InitEffects()
        {
            if (SteamAudioManager.Singleton == null || SteamAudioManager.Context == null) return;

            IntPtr ctx = SteamAudioManager.Context.Get();
            IntPtr hrtf = SteamAudioManager.CurrentHRTF?.Get() ?? IntPtr.Zero;
            if (ctx == IntPtr.Zero || hrtf == IntPtr.Zero) return;

            int rate, frameSize;
#if STEAMAUDIO_ENABLED
            var saAudio = SteamAudioManager.AudioSettings;
            rate = saAudio.samplingRate;
            frameSize = saAudio.frameSize;
#else
            rate = UnityEngine.AudioSettings.outputSampleRate;
            UnityEngine.AudioSettings.GetDSPBufferSize(out frameSize, out _);
#endif
            var audio = new PAudioSettings { samplingRate = rate, frameSize = frameSize };

            // binaural
            if (_binaural == IntPtr.Zero)
            {
                var binS = new PBinauralEffectSettings { hrtf = hrtf };
                int r = iplBinauralEffectCreate(ctx, ref audio, ref binS, out _binaural);
                if (r != 0 || _binaural == IntPtr.Zero)
                {
                    LogW($"iplBinauralEffectCreate failed (err {r}).");
                    _binaural = IntPtr.Zero;
                    return; // retry next frame
                }
                LogV("BinauralEffect created.");
            }

            // direct
            if (_direct == IntPtr.Zero)
            {
                var dirS = new PDirectEffectSettings { numChannels = 1 };
                iplDirectEffectCreate(ctx, ref audio, ref dirS, out _direct);
                LogV("DirectEffect created.");
            }

            // reflection + ambisonics
            if (SteamAudioManager.Simulator == null) return;

            // Guard: only allocate the expensive reflection pipeline (IR buffers ~700 KB each)
            // when this source actually has reflections enabled.
            // With 400 sources all initialising at once, skipping when reflections=false
            // prevents IPL_STATUS_OUTOFMEMORY (err 2) from iplReflectionEffectCreate.
#if STEAMAUDIO_ENABLED
            if (_steamSrc == null || !_steamSrc.reflections) return;
#else
            return;
#endif

            var settings = SteamAudioSettings.Singleton;
            if (settings == null) return;

            int maxOrder = settings.realTimeAmbisonicOrder;
            int ambiCh = (maxOrder + 1) * (maxOrder + 1);
            int irSz = Mathf.Max(1, (int)(settings.realTimeDuration * rate));
            int reflType = (int)settings.reflectionEffectType;

            if (_reflectionEffect == IntPtr.Zero)
            {
                var reflS = new PReflectionEffectSettings
                {
                    type = reflType,
                    numChannels = ambiCh,
                    irSize = irSz,
                };
                int r = iplReflectionEffectCreate(ctx, ref audio, ref reflS, out _reflectionEffect);
                if (r != 0)
                {
                    LogW($"iplReflectionEffectCreate failed (err {r}).");
                    _reflectionEffect = IntPtr.Zero;
                }
                else LogV($"ReflectionEffect created (order={maxOrder}, irSize={irSz}).");
            }

            if (_ambiDecodeEffect == IntPtr.Zero)
            {
                var ambiS = new PAmbisonicsDecodeEffectSettings
                {
                    speakerLayout = new PSpeakerLayout
                    {
                        type = 1, // IPL_SPEAKERLAYOUTTYPE_STEREO
                        numSpeakers = 0,
                        speakers = IntPtr.Zero,
                    },
                    hrtf = hrtf,
                    maxOrder = maxOrder,
                };
                int r = iplAmbisonicsDecodeEffectCreate(ctx, ref audio, ref ambiS, out _ambiDecodeEffect);
                if (r != 0)
                {
                    LogW($"iplAmbisonicsDecodeEffectCreate failed (err {r}).");
                    _ambiDecodeEffect = IntPtr.Zero;
                }
                else LogV("AmbisonicsDecodeEffect created.");
            }

            // alocate native audio buffers for reflection pipeline
            if (!_reflBufsAllocated && _reflectionEffect != IntPtr.Zero && _ambiDecodeEffect != IntPtr.Zero)
            {
                PAudioBuffer localAmbi, localStereo;
                int r1 = iplAudioBufferAllocate(ctx, ambiCh, frameSize, out localAmbi);
                int r2 = iplAudioBufferAllocate(ctx, 2, frameSize, out localStereo);

                if (r1 == 0 && r2 == 0)
                {
                    _cachedContext = ctx;
                    // store pipeline params before publishing the flag
                    _cachedMaxOrder = maxOrder;
                    _maxAmbiChannels = ambiCh;
                    _irSize = irSz;
                    _nativeFrameSize = frameSize;
                    _reflAmbiNative = localAmbi;
                    _reflStereoNative = localStereo;

                    // publish: volatile write acts as a full memory fence.
                    _reflBufsAllocated = true;
                    LogV($"Reflection buffers allocated ({ambiCh}ch ambi + stereo, {frameSize} frames).");
                }
                else
                {
                    LogW($"iplAudioBufferAllocate failed: r1={r1}, r2={r2}");
                    if (r1 == 0) iplAudioBufferFree(ctx, ref localAmbi);
                    if (r2 == 0) iplAudioBufferFree(ctx, ref localStereo);
                }
            }
        }


        private void Update()
        {
            // Lazy re-init: retry every frame until binaural effect is created
            // (handles the case where HRTF wasn't ready at Awake time).
            if (_binaural == IntPtr.Zero)
            {
                InitEffects();
                if (_binaural == IntPtr.Zero) return; // still not ready
            }

            // Lazily bring up the reflection pipeline once the Simulator is running
            // AND this source actually has reflections enabled.
            // The _steamSrc.reflections guard here mirrors the one inside InitEffects()
            // and avoids calling into it every frame for sources that never need reflections.
#if STEAMAUDIO_ENABLED
            if (!_reflBufsAllocated && SteamAudioManager.Simulator != null
                && _steamSrc != null && _steamSrc.reflections)
#else
            if (false)
#endif
            {
                InitEffects();
            }

            Transform listener = GetListenerTransform();
            if (listener == null) return;

            // direction (source relative to listener, in listener space)
            UnityEngine.Vector3 localPos = listener.InverseTransformPoint(transform.position);
            UnityEngine.Vector3 d = localPos.sqrMagnitude < 1e-6f
                ? UnityEngine.Vector3.forward
                : localPos.normalized;

            float dist = (transform.position - listener.position).magnitude;

            // Occlusion / transmission / distance attenuation
            bool shouldApplyDistAtten = true;
            float occ = 1f, tLow = 0f, tMid = 0f, tHigh = 0f;
            bool applyTrans = false;

            // Reflection data
            bool hasRefl = false;
            ReflectionEffectParams reflParams = default;
            PCoordinateSpace3 listenerCS = default;
            float reflMix = 1f;

#if STEAMAUDIO_ENABLED
            if (_steamSrc != null)
            {
                shouldApplyDistAtten = _steamSrc.distanceAttenuation;
                occ = Mathf.Clamp01(_steamSrc.occlusionValue);

                applyTrans = _steamSrc.transmission;
                if (applyTrans)
                {
                    tLow = Mathf.Clamp01(_steamSrc.transmissionLow);
                    tMid = Mathf.Clamp01(_steamSrc.transmissionMid);
                    tHigh = Mathf.Clamp01(_steamSrc.transmissionHigh);
                }

                // Reflection simulation outputs
                if (_steamSrc.reflections && _reflBufsAllocated && _reflectionEffect != IntPtr.Zero)
                {
                    try
                    {
                        var outputs = _steamSrc.GetOutputs(SimulationFlags.Reflections);
                        var saSettings = SteamAudioSettings.Singleton;

                        // Copy the IR handle + EQ from simulation; override bookkeeping fields.
                        reflParams = outputs.reflections;
                        reflParams.type = saSettings.reflectionEffectType;
                        reflParams.numChannels = _maxAmbiChannels;
                        reflParams.irSize = _irSize;

                        hasRefl = reflParams.ir != IntPtr.Zero;
                        reflMix = Mathf.Clamp(_steamSrc.reflectionsMixLevel, 0f, 10f);

                        // Listener coordinate space (Unity left-handed → Phonon right-handed: flip Z)
                        UnityEngine.Vector3 lR = listener.right;
                        UnityEngine.Vector3 lU = listener.up;
                        UnityEngine.Vector3 lF = listener.forward;
                        UnityEngine.Vector3 lP = listener.position;

                        listenerCS = new PCoordinateSpace3
                        {
                            right = new PVec3 { x = lR.x, y = lR.y, z = -lR.z },
                            up = new PVec3 { x = lU.x, y = lU.y, z = -lU.z },
                            ahead = new PVec3 { x = lF.x, y = lF.y, z = -lF.z },
                            origin = new PVec3 { x = lP.x, y = lP.y, z = -lP.z },
                        };
                    }
                    catch (Exception ex)
                    {
                        LogW($"GetOutputs(Reflections) threw: {ex.Message}");
                    }
                }
            }
#endif

            float atten = shouldApplyDistAtten ? CalculateDistanceAttenuation(dist) : 1f;
            IntPtr hrtfPtr = SteamAudioManager.CurrentHRTF?.Get() ?? IntPtr.Zero;

            lock (_lock)
            {
                _dir = new PVec3 { x = d.x, y = d.y, z = -d.z };
                _distAtten = atten;
                _occlusion = occ;
                _transLow = tLow;
                _transMid = tMid;
                _transHigh = tHigh;
                _applyTransmission = applyTrans;
                _hrtf = hrtfPtr;

                _hasValidReflData = hasRefl;
                _cachedReflParams = reflParams;
                _listenerCS = listenerCS;
                _reflMixLevel = reflMix;
            }
        }

        // AUDIO THREAD
        private unsafe void OnAudioFilterRead(float[] data, int channels)
        {
            if (IsBypass || channels != 2 || _binaural == IntPtr.Zero)
            {
                // if (IsBypass) Array.Clear(data, 0, data.Length);
                return;
            }

            lock (_lock)
            {
                int n = data.Length / channels;

                // grow managed scratch buffers if Unity's DSP buffer expanded
                if (n > _monoIn.Length)
                {
                    _monoIn = new float[n];
                    _monoOut = new float[n];
                    _leftOut = new float[n];
                    _rightOut = new float[n];
                }

                float blend = spatialize ? Mathf.Clamp01(spatialBlend) : 0;
                // float blend = Mathf.Clamp01(spatialBlend);

                float effectiveAtten = Mathf.Lerp(1f, _distAtten, blend);

                // downmix stereo -> mono with distance attenuation
                for (int i = 0; i < n; i++)
                    _monoIn[i] = (data[i * channels] + data[i * channels + 1]) * 0.5f * effectiveAtten;

                fixed (float* pIn = _monoIn,
                              pOut = _monoOut,
                              pLeft = _leftOut,
                              pRight = _rightOut)
                {
                    // build PAudioBuffer views over managed memory (no native allocation here)
                    IntPtr* inPtrs = stackalloc IntPtr[1]; inPtrs[0] = (IntPtr)pIn;
                    IntPtr* outPtrs = stackalloc IntPtr[1]; outPtrs[0] = (IntPtr)pOut;
                    IntPtr* binPtrs = stackalloc IntPtr[2]; binPtrs[0] = (IntPtr)pLeft;
                    binPtrs[1] = (IntPtr)pRight;

                    var inBuf = new PAudioBuffer { numChannels = 1, numSamples = n, data = (IntPtr)inPtrs };
                    var outBuf = new PAudioBuffer { numChannels = 1, numSamples = n, data = (IntPtr)outPtrs };
                    var binBuf = new PAudioBuffer { numChannels = 2, numSamples = n, data = (IntPtr)binPtrs };

                    // STAGE 1: DirectEffect — occlusion + transmission
                    if (_direct != IntPtr.Zero)
                    {
                        int dflags = (int)DirectEffectFlags.ApplyOcclusion;
                        if (_applyTransmission) dflags |= (int)DirectEffectFlags.ApplyTransmission;

                        var dp = new PDirectEffectParams
                        {
                            flags = dflags,
                            transmissionType = 1,          // FrequencyDependent
                            distanceAttenuation = 1f,
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

                    // STAGE 2: BinauralEffect - HRTF on the direct signal (Happens when we load a scene and it contains static geometry)
                    if (_hrtf != IntPtr.Zero)
                    {
                        var bp = new PBinauralEffectParams
                        {
                            direction = _dir,
                            interpolation = 1,   // bilinear
                            spatialBlend = blend,
                            hrtf = _hrtf,
                            peakDelays = IntPtr.Zero,
                        };
                        iplBinauralEffectApply(_binaural, ref bp, ref outBuf, ref binBuf);
                    }
                    else
                    {
                        // No HRTF yet: pass-through to both channels
                        for (int i = 0; i < n; i++)
                        {
                            _leftOut[i] = _monoOut[i];
                            _rightOut[i] = _monoOut[i];
                        }
                    }

                    // STAGE 3: ReflectionEffect + AmbisonicsDecodeEffect
                    // Only when simulation has produced valid IR data and native
                    // buffers are allocated for the correct frame size.
                    bool doReflections =
                        _hasValidReflData &&
                        _reflBufsAllocated &&
                        _reflectionEffect != IntPtr.Zero &&
                        _ambiDecodeEffect != IntPtr.Zero &&
                        _hrtf != IntPtr.Zero &&
                        n == _nativeFrameSize &&
                        _reflAmbiNative.data != IntPtr.Zero &&
                        _reflStereoNative.data != IntPtr.Zero;

                    if (doReflections)
                    {
                        // convolve mono input with the room IR -> ambisonic buffer
                        var reflP = _cachedReflParams;
                        iplReflectionEffectApply(
                            _reflectionEffect,
                            ref reflP,
                            ref inBuf,
                            ref _reflAmbiNative,
                            IntPtr.Zero); // no shared mixer

                        // decode ambisonics -> binaural stereo using HRTF
                        var ambiP = new PAmbisonicsDecodeEffectParams
                        {
                            order = _cachedMaxOrder,
                            hrtf = _hrtf,
                            orientation = _listenerCS,
                            binaural = 1, // IPL_TRUE: apply HRTF
                        };
                        iplAmbisonicsDecodeEffectApply(
                            _ambiDecodeEffect,
                            ref ambiP,
                            ref _reflAmbiNative,
                            ref _reflStereoNative);

                        // additively mix decoded reflections into the direct binaural output
                        float** reflChPtrs = (float**)_reflStereoNative.data.ToPointer();
                        float* reflL = reflChPtrs[0];
                        float* reflR = reflChPtrs[1];
                        float mix = _reflMixLevel;

                        for (int i = 0; i < n; i++)
                        {
                            _leftOut[i] += reflL[i] * mix;
                            _rightOut[i] += reflR[i] * mix;
                        }
                    }
                }

                // write final stereo back to Unity's interleaved output buffer
                for (int i = 0; i < n; i++)
                {
                    data[i * channels] = _leftOut[i];
                    data[i * channels + 1] = _rightOut[i];
                }
            }
        }

        // distance attentuation
        private static Transform _cachedListener;
        private static int _lastListenerSearchFrame = -1;

        private Transform GetListenerTransform()
        {
            if (_cachedListener != null && _cachedListener.gameObject.activeInHierarchy)
                return _cachedListener;

            // Restricts the heavy Unity Scene Hierarchy dive to a maximum of ONCE per frame
            if (Time.frameCount == _lastListenerSearchFrame)
                return _cachedListener;

            _lastListenerSearchFrame = Time.frameCount;

            var cam = Camera.main;
            if (cam != null)
            {
                _cachedListener = cam.transform;
                return _cachedListener;
            }

            var al = FindObjectOfType<AudioListener>();
            if (al != null)
            {
                _cachedListener = al.transform;
                return _cachedListener;
            }

            _cachedListener = null;
            return null;
        }

        private float CalculateDistanceAttenuation(float dist)
        {
            if (_src == null) return 0f;

            float minDist = _src.minDistance;
            float maxDist = Mathf.Max(_src.maxDistance, minDist + 0.001f);

            if (verboseLogging) LogV($"  Dist atten for {dist:F2}m (Min={minDist:F1}, Max={maxDist:F1}, mode={_src.rolloffMode})");

            if (dist <= minDist) return 1f;
            if (dist >= maxDist) return 0f;

            if (_src.rolloffMode == AudioRolloffMode.Custom)
            {
                if (_cachedRolloffCurve == null || _lastMaxDist != maxDist)
                {
                    if (verboseLogging) LogV("  Recaching custom rolloff curve.");
                    _cachedRolloffCurve = _src.GetCustomCurve(AudioSourceCurveType.CustomRolloff);
                    _lastMaxDist = maxDist;

                    if (_cachedRolloffCurve == null || _cachedRolloffCurve.length == 0)
                    {
                        if (verboseLogging) LogW("  Custom curve NULL or empty – falling back to linear.");
                        return 1f - (dist - minDist) / (maxDist - minDist);
                    }
                }

                float norm = dist / maxDist;
                float result = Mathf.Clamp01(_cachedRolloffCurve.Evaluate(norm));

                if (verboseLogging) LogV($"  Custom curve: Evaluate({norm:F3}) = {result:F3}");
                return result;
            }

            if (_src.rolloffMode == AudioRolloffMode.Linear)
            {
                float result = 1f - (dist - minDist) / (maxDist - minDist);

                if (verboseLogging) LogV($"  Linear: {result:F3}");
                return result;
            }

            if (_src.rolloffMode == AudioRolloffMode.Logarithmic)
            {
                float result = minDist / dist;

                if (verboseLogging) LogV($"  Logarithmic: {result:F3}");
                return result;
            }

            return 1f;
        }
#if UNITY_EDITOR
        private void OnValidate()
        {
            if (verboseLogging)
                Debug.Log($"[PhononDSPBridge] verboseLogging enabled on '{gameObject?.name}'.");
        }
#endif
    }

    // ── P/Invoke structs ──────────────────────────────────────────────────────

    [StructLayout(LayoutKind.Sequential)]
    internal struct PAudioSettings
    {
        public int samplingRate;
        public int frameSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PVec3
    {
        public float x, y, z;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PBinauralEffectSettings
    {
        public IntPtr hrtf;
    }

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
    internal struct PDirectEffectSettings
    {
        public int numChannels;
    }

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
        public int type;        // IPLReflectionEffectType
        public int numChannels; // (maxOrder+1)^2
        public int irSize;      // maxDuration * samplingRate
    }

    /// <summary>Matches IPLSpeakerLayout in phonon.h.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct PSpeakerLayout
    {
        public int type;        // IPLSpeakerLayoutType: 1 = Stereo
        public int numSpeakers; // 0 for standard layouts
        public IntPtr speakers;    // nullptr for standard layouts
    }

    /// <summary>Matches IPLAmbisonicsDecodeEffectSettings in phonon.h.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct PAmbisonicsDecodeEffectSettings
    {
        public PSpeakerLayout speakerLayout;
        public IntPtr hrtf;
        public int maxOrder;
    }

    /// <summary>
    /// Matches IPLCoordinateSpace3 in phonon.h:
    /// right (12) + up (12) + ahead (12) + origin (12) = 48 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct PCoordinateSpace3
    {
        public PVec3 right;
        public PVec3 up;
        public PVec3 ahead;
        public PVec3 origin;
    }

    /// <summary>Matches IPLAmbisonicsDecodeEffectParams in phonon.h.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct PAmbisonicsDecodeEffectParams
    {
        public int order;
        public IntPtr hrtf;
        public PCoordinateSpace3 orientation;
        public int binaural; // IPLBool: 0 = false, 1 = true
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PAudioBuffer
    {
        public int numChannels;
        public int numSamples;
        public IntPtr data; // float** — one float* per channel
    }
}

using System;
using UnityEngine;
using SteamAudio;

namespace ifp.arena.shared
{
    [RequireComponent(typeof(AudioSource))]
    public class PhononDSPBridge : MonoBehaviour
    {
        // ── Effect handles ────────────────────────────────────────────────────
        private IntPtr _binaural = IntPtr.Zero;
        private IntPtr _direct = IntPtr.Zero;
        private IntPtr _reflectionEffect = IntPtr.Zero;
        private IntPtr _ambiDecodeEffect = IntPtr.Zero;

        // ── Native-allocated buffers ──────────────────────────────────────────
        private PAudioBuffer _reflAmbiNative;
        private PAudioBuffer _reflStereoNative;
        private volatile bool _reflBufsAllocated = false;

        private int _maxAmbiChannels = 4;
        private int _irSize = 0;
        private int _nativeFrameSize = 0;
        private int _cachedMaxOrder = 1;

        // ── Components & Utils ────────────────────────────────────────────────
        private AudioSource _src;
#if STEAMAUDIO_ENABLED
        private SteamAudioSource _steamSrc;
#endif
        private PhononDistanceAttenuator _attenuator;
        private IntPtr _cachedContext = IntPtr.Zero;

        // ── Per-Frame Thread Data ─────────────────────────────────────────────
        private PVec3 _dir = new PVec3 { z = 1f };
        private float _distAtten = 1f;
        private float _occlusion = 1f;
        private float _transLow = 0f, _transMid = 0f, _transHigh = 0f;
        private bool _applyTransmission = false;
        private IntPtr _hrtf = IntPtr.Zero;

        private ReflectionEffectParams _cachedReflParams;
        private bool _hasValidReflData = false;
        private PCoordinateSpace3 _listenerCS;
        private float _reflMixLevel = 1f;

        private readonly object _lock = new object();

        // ── Managed Scratch Buffers ───────────────────────────────────────────
        private float[] _monoIn;
        private float[] _monoOut;
        private float[] _leftOut;
        private float[] _rightOut;

        // ── Public Settings ───────────────────────────────────────────────────
        public float spatialBlend = 1f;
        public bool spatialize = true;

        public bool IsBypass = false;

        private void Awake()
        {
            _src = GetComponent<AudioSource>();
            _attenuator = new PhononDistanceAttenuator(_src);

#if STEAMAUDIO_ENABLED
            _steamSrc = GetComponent<SteamAudioSource>();
#endif

            UnityEngine.AudioSettings.GetDSPBufferSize(out int bufSize, out _);
            int cap = bufSize * 2;
            _monoIn = new float[cap];
            _monoOut = new float[cap];
            _leftOut = new float[cap];
            _rightOut = new float[cap];

            InitEffects();
        }

        private void OnDestroy()
        {
            lock (_lock)
            {
                if (_binaural != IntPtr.Zero) { PhononNative.iplBinauralEffectRelease(ref _binaural); _binaural = IntPtr.Zero; }
                if (_direct != IntPtr.Zero) { PhononNative.iplDirectEffectRelease(ref _direct); _direct = IntPtr.Zero; }
                if (_reflectionEffect != IntPtr.Zero) { PhononNative.iplReflectionEffectRelease(ref _reflectionEffect); _reflectionEffect = IntPtr.Zero; }
                if (_ambiDecodeEffect != IntPtr.Zero) { PhononNative.iplAmbisonicsDecodeEffectRelease(ref _ambiDecodeEffect); _ambiDecodeEffect = IntPtr.Zero; }

                if (_reflBufsAllocated)
                {
                    _reflBufsAllocated = false;
                    if (_cachedContext != IntPtr.Zero)
                    {
                        if (_reflAmbiNative.data != IntPtr.Zero) PhononNative.iplAudioBufferFree(_cachedContext, ref _reflAmbiNative);
                        if (_reflStereoNative.data != IntPtr.Zero) PhononNative.iplAudioBufferFree(_cachedContext, ref _reflStereoNative);
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

            if (_binaural == IntPtr.Zero)
            {
                var binS = new PBinauralEffectSettings { hrtf = hrtf };
                int r = PhononNative.iplBinauralEffectCreate(ctx, ref audio, ref binS, out _binaural);
                if (r != 0 || _binaural == IntPtr.Zero) return;
            }

            if (_direct == IntPtr.Zero)
            {
                var dirS = new PDirectEffectSettings { numChannels = 1 };
                PhononNative.iplDirectEffectCreate(ctx, ref audio, ref dirS, out _direct);
            }

            if (SteamAudioManager.Simulator == null) return;

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
                var reflS = new PReflectionEffectSettings { type = reflType, numChannels = ambiCh, irSize = irSz };
                if (PhononNative.iplReflectionEffectCreate(ctx, ref audio, ref reflS, out _reflectionEffect) != 0)
                    _reflectionEffect = IntPtr.Zero;
            }

            if (_ambiDecodeEffect == IntPtr.Zero)
            {
                var ambiS = new PAmbisonicsDecodeEffectSettings
                {
                    speakerLayout = new PSpeakerLayout { type = 1, numSpeakers = 0, speakers = IntPtr.Zero },
                    hrtf = hrtf,
                    maxOrder = maxOrder,
                };
                if (PhononNative.iplAmbisonicsDecodeEffectCreate(ctx, ref audio, ref ambiS, out _ambiDecodeEffect) != 0)
                    _ambiDecodeEffect = IntPtr.Zero;
            }

            if (!_reflBufsAllocated && _reflectionEffect != IntPtr.Zero && _ambiDecodeEffect != IntPtr.Zero)
            {
                int r1 = PhononNative.iplAudioBufferAllocate(ctx, ambiCh, frameSize, out PAudioBuffer localAmbi);
                int r2 = PhononNative.iplAudioBufferAllocate(ctx, 2, frameSize, out PAudioBuffer localStereo);

                if (r1 == 0 && r2 == 0)
                {
                    _cachedContext = ctx;
                    _cachedMaxOrder = maxOrder;
                    _maxAmbiChannels = ambiCh;
                    _irSize = irSz;
                    _nativeFrameSize = frameSize;
                    _reflAmbiNative = localAmbi;
                    _reflStereoNative = localStereo;

                    _reflBufsAllocated = true; // volatile write
                }
                else
                {
                    if (r1 == 0) PhononNative.iplAudioBufferFree(ctx, ref localAmbi);
                    if (r2 == 0) PhononNative.iplAudioBufferFree(ctx, ref localStereo);
                }
            }
        }

        private void Update()
        {
            if (_binaural == IntPtr.Zero)
            {
                InitEffects();
                if (_binaural == IntPtr.Zero) return;
            }

#if STEAMAUDIO_ENABLED
            if (!_reflBufsAllocated && SteamAudioManager.Simulator != null && _steamSrc != null && _steamSrc.reflections)
                InitEffects();
#endif

            Transform listener = PhononListenerCache.GetListenerTransform();
            if (listener == null) return;

            UnityEngine.Vector3 localPos = listener.InverseTransformPoint(transform.position);
            UnityEngine.Vector3 d = localPos.sqrMagnitude < 1e-6f ? UnityEngine.Vector3.forward : localPos.normalized;
            float dist = (transform.position - listener.position).magnitude;

            bool shouldApplyDistAtten = true;
            float occ = 1f, tLow = 0f, tMid = 0f, tHigh = 0f;
            bool applyTrans = false;

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

                if (_steamSrc.reflections && _reflBufsAllocated && _reflectionEffect != IntPtr.Zero)
                {
                    try
                    {
                        var outputs = _steamSrc.GetOutputs(SimulationFlags.Reflections);
                        var saSettings = SteamAudioSettings.Singleton;

                        reflParams = outputs.reflections;
                        reflParams.type = saSettings.reflectionEffectType;
                        reflParams.numChannels = _maxAmbiChannels;
                        reflParams.irSize = _irSize;

                        hasRefl = reflParams.ir != IntPtr.Zero;
                        reflMix = Mathf.Clamp(_steamSrc.reflectionsMixLevel, 0f, 10f);

                        UnityEngine.Vector3 lR = listener.right, lU = listener.up, lF = listener.forward, lP = listener.position;
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
                        Debug.LogError($"GetOutputs(Reflections) threw: {ex.Message}");
                    }
                }
            }
#endif

            float atten = shouldApplyDistAtten ? _attenuator.Calculate(dist) : 1f;
            IntPtr hrtfPtr = SteamAudioManager.CurrentHRTF?.Get() ?? IntPtr.Zero;

            lock (_lock)
            {
                _dir = new PVec3 { x = d.x, y = d.y, z = -d.z };
                _distAtten = atten;
                _occlusion = occ;
                _transLow = tLow; _transMid = tMid; _transHigh = tHigh;
                _applyTransmission = applyTrans;
                _hrtf = hrtfPtr;

                _hasValidReflData = hasRefl;
                _cachedReflParams = reflParams;
                _listenerCS = listenerCS;
                _reflMixLevel = reflMix;
            }
        }

        private unsafe void OnAudioFilterRead(float[] data, int channels)
        {
            if (IsBypass || channels != 2) return;

            lock (_lock)
            {
                if (_binaural == IntPtr.Zero) return;

                int n = data.Length / channels;

                if (n > _monoIn.Length)
                {
                    _monoIn = new float[n]; _monoOut = new float[n];
                    _leftOut = new float[n]; _rightOut = new float[n];
                }

                float blend = Mathf.Clamp01(spatialBlend);
                float effectiveAtten = Mathf.Lerp(1f, _distAtten, blend);

                for (int i = 0; i < n; i++)
                    _monoIn[i] = (data[i * channels] + data[i * channels + 1]) * 0.5f * effectiveAtten;

                fixed (float* pIn = _monoIn, pOut = _monoOut, pLeft = _leftOut, pRight = _rightOut)
                {
                    IntPtr* inPtrs = stackalloc IntPtr[1]; inPtrs[0] = (IntPtr)pIn;
                    IntPtr* outPtrs = stackalloc IntPtr[1]; outPtrs[0] = (IntPtr)pOut;
                    IntPtr* binPtrs = stackalloc IntPtr[2]; binPtrs[0] = (IntPtr)pLeft; binPtrs[1] = (IntPtr)pRight;

                    var inBuf = new PAudioBuffer { numChannels = 1, numSamples = n, data = (IntPtr)inPtrs };
                    var outBuf = new PAudioBuffer { numChannels = 1, numSamples = n, data = (IntPtr)outPtrs };
                    var binBuf = new PAudioBuffer { numChannels = 2, numSamples = n, data = (IntPtr)binPtrs };

                    // STAGE 1
                    if (_direct != IntPtr.Zero)
                    {
                        int dflags = (int)DirectEffectFlags.ApplyOcclusion;
                        if (_applyTransmission) dflags |= (int)DirectEffectFlags.ApplyTransmission;

                        var dp = new PDirectEffectParams
                        {
                            flags = dflags,
                            transmissionType = 1,
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
                        PhononNative.iplDirectEffectApply(_direct, ref dp, ref inBuf, ref outBuf);
                    }
                    else
                    {
                        for (int i = 0; i < n; i++) _monoOut[i] = _monoIn[i];
                    }

                    // STAGE 2
                    if (_hrtf != IntPtr.Zero)
                    {
                        var bp = new PBinauralEffectParams
                        {
                            direction = _dir,
                            interpolation = 1,
                            spatialBlend = blend,
                            hrtf = _hrtf,
                            peakDelays = IntPtr.Zero,
                        };
                        PhononNative.iplBinauralEffectApply(_binaural, ref bp, ref outBuf, ref binBuf);
                    }
                    else
                    {
                        for (int i = 0; i < n; i++) { _leftOut[i] = _monoOut[i]; _rightOut[i] = _monoOut[i]; }
                    }

                    // STAGE 3
                    bool doReflections = _hasValidReflData && _reflBufsAllocated && _reflectionEffect != IntPtr.Zero &&
                                         _ambiDecodeEffect != IntPtr.Zero && _hrtf != IntPtr.Zero && n == _nativeFrameSize &&
                                         _reflAmbiNative.data != IntPtr.Zero && _reflStereoNative.data != IntPtr.Zero;

                    if (doReflections)
                    {
                        var reflP = _cachedReflParams;
                        PhononNative.iplReflectionEffectApply(_reflectionEffect, ref reflP, ref inBuf, ref _reflAmbiNative, IntPtr.Zero);

                        var ambiP = new PAmbisonicsDecodeEffectParams
                        {
                            order = _cachedMaxOrder,
                            hrtf = _hrtf,
                            orientation = _listenerCS,
                            binaural = 1,
                        };
                        PhononNative.iplAmbisonicsDecodeEffectApply(_ambiDecodeEffect, ref ambiP, ref _reflAmbiNative, ref _reflStereoNative);

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

                for (int i = 0; i < n; i++)
                {
                    data[i * channels] = _leftOut[i];
                    data[i * channels + 1] = _rightOut[i];
                }
            }
        }
    }
}
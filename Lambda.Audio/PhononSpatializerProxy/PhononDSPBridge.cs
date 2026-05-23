using System;
using System.Threading;
using UnityEngine;
using SteamAudio;

namespace PhononSpatializerProxy
{
    [RequireComponent(typeof(AudioSource))]
    [RequireComponent(typeof(SteamAudioSource))]
    public class PhononDSPBridge : MonoBehaviour, IProxiedAudioSource
    {
        private struct DSPParams
        {
            public PVec3 Dir;
            public float DistAtten;
            public float Occlusion;
            public float TransLow;
            public float TransMid;
            public float TransHigh;
            public bool ApplyTransmission;
            public IntPtr Hrtf;

            public ReflectionEffectParams ReflParams;
            public bool HasValidReflData;
            public PCoordinateSpace3 ListenerCS;
            public float ReflMixLevel;

            public float SpatialBlend;
        }

        private DSPParams _currentParams;

        private int _paramsLock = 0;

        private IntPtr _binaural = IntPtr.Zero;
        private IntPtr _direct = IntPtr.Zero;
        private IntPtr _reflectionEffect = IntPtr.Zero;
        private IntPtr _ambiDecodeEffect = IntPtr.Zero;

        private PAudioBuffer _reflAmbiNative;
        private PAudioBuffer _reflStereoNative;
        private volatile bool _reflBufsAllocated = false;

        private int _maxAmbiChannels = 4;
        private int _irSize = 0;
        private int _nativeFrameSize = 0;
        private int _cachedMaxOrder = 1;

        private AudioSource _src;
        private SteamAudioSource _steamSrc;
        private PhononDistanceAttenuator _attenuator;
        private IntPtr _cachedContext = IntPtr.Zero;

        private readonly object _lock = new object();

        private float[] _monoIn;
        private float[] _monoOut;
        private float[] _leftOut;
        private float[] _rightOut;

        // Unity proxy
        public float spatialBlend { get; set; } = 1f;
        public bool spatialize { get; set; } = true;

        // [SerializeField] private float spatialBlend = 1f;
        // [SerializeField] private bool spatialize = true;

        // public float SpatialBlend
        // {
        //     get => spatialBlend;
        //     set => spatialBlend = value;
        // }

        // public bool Spatialize
        // {
        //     get => spatialize;
        //     set => spatialize = value;
        // }

        public bool isBypass { get; set; } = false;

        private volatile bool _bufferOverflowed;

        private void EnterParamsLock()
        {
            var spinWait = new SpinWait();
            while (Interlocked.CompareExchange(ref _paramsLock, 1, 0) != 0)
                spinWait.SpinOnce();
        }

        private void ExitParamsLock()
        {
            Volatile.Write(ref _paramsLock, 0);
        }

        private void Awake()
        {
            _src = GetComponent<AudioSource>();
            _attenuator = new PhononDistanceAttenuator(_src);
            _steamSrc = GetComponent<SteamAudioSource>();

            int safeMaxCapacity = 8192;
            _monoIn = new float[safeMaxCapacity];
            _monoOut = new float[safeMaxCapacity];
            _leftOut = new float[safeMaxCapacity];
            _rightOut = new float[safeMaxCapacity];

            _currentParams = new DSPParams
            {
                Dir = new PVec3 { z = 1f },
                DistAtten = 1f,
                Occlusion = 1f,
                ReflMixLevel = 1f,
                SpatialBlend = 1f
            };

            InitEffects();
        }

        private void OnDestroy()
        {
            lock (_lock)
            {
                if (_binaural != IntPtr.Zero) PhononNative.iplBinauralEffectRelease(ref _binaural); _binaural = IntPtr.Zero;
                if (_direct != IntPtr.Zero) PhononNative.iplDirectEffectRelease(ref _direct); _direct = IntPtr.Zero;
                if (_reflectionEffect != IntPtr.Zero) PhononNative.iplReflectionEffectRelease(ref _reflectionEffect); _reflectionEffect = IntPtr.Zero;
                if (_ambiDecodeEffect != IntPtr.Zero) PhononNative.iplAmbisonicsDecodeEffectRelease(ref _ambiDecodeEffect); _ambiDecodeEffect = IntPtr.Zero;

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
            lock (_lock)
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
                    var reflS = new PReflectionEffectSettings
                    {
                        type = reflType,
                        numChannels = ambiCh,
                        irSize = irSz
                    };

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

            UnityEngine.Vector3 sourcePos = transform.position;

            listener.GetPositionAndRotation(out UnityEngine.Vector3 listenerPos, out UnityEngine.Quaternion listenerRot);

            UnityEngine.Vector3 listenerRight = listenerRot * UnityEngine.Vector3.right;
            UnityEngine.Vector3 listenerUp = listenerRot * UnityEngine.Vector3.up;
            UnityEngine.Vector3 listenerForward = listenerRot * UnityEngine.Vector3.forward;

            UnityEngine.Vector3 diff = sourcePos - listenerPos;
            UnityEngine.Vector3 localPos = Quaternion.Inverse(listenerRot) * diff;
            UnityEngine.Vector3 d = localPos.sqrMagnitude < 1e-6f ? UnityEngine.Vector3.forward : localPos.normalized;

            float dist = diff.magnitude;

            DSPParams newParams = default;
            newParams.Dir = new PVec3 { x = d.x, y = d.y, z = -d.z };
            newParams.SpatialBlend = spatialBlend;
            newParams.DistAtten = 1f;
            newParams.Occlusion = 1f;
            newParams.ReflMixLevel = 1f;

#if STEAMAUDIO_ENABLED
            if (_steamSrc != null)
            {
                newParams.DistAtten = _steamSrc.distanceAttenuation ? _attenuator.Calculate(dist) : 1f;
                newParams.Occlusion = Mathf.Clamp01(_steamSrc.occlusionValue);
                newParams.ApplyTransmission = _steamSrc.transmission;
                if (newParams.ApplyTransmission)
                {
                    newParams.TransLow = Mathf.Clamp01(_steamSrc.transmissionLow);
                    newParams.TransMid = Mathf.Clamp01(_steamSrc.transmissionMid);
                    newParams.TransHigh = Mathf.Clamp01(_steamSrc.transmissionHigh);
                }

                if (_steamSrc.reflections && _reflBufsAllocated && _reflectionEffect != IntPtr.Zero)
                {
                    try
                    {
                        var outputs = _steamSrc.GetOutputs(SimulationFlags.Reflections);
                        var saSettings = SteamAudioSettings.Singleton;

                        newParams.ReflParams = outputs.reflections;
                        newParams.ReflParams.type = saSettings.reflectionEffectType;
                        newParams.ReflParams.numChannels = _maxAmbiChannels;
                        newParams.ReflParams.irSize = _irSize;

                        newParams.HasValidReflData = newParams.ReflParams.ir != IntPtr.Zero;
                        newParams.ReflMixLevel = Mathf.Clamp(_steamSrc.reflectionsMixLevel, 0f, 10f);

                        newParams.ListenerCS = new PCoordinateSpace3
                        {
                            right = new PVec3 { x = listenerRight.x, y = listenerRight.y, z = -listenerRight.z },
                            up = new PVec3 { x = listenerUp.x, y = listenerUp.y, z = -listenerUp.z },
                            ahead = new PVec3 { x = listenerForward.x, y = listenerForward.y, z = -listenerForward.z },
                            origin = new PVec3 { x = listenerPos.x, y = listenerPos.y, z = -listenerPos.z },
                        };
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"GetOutputs(Reflections) threw: {ex.Message}");
                    }
                }
            }
            else
            {
                newParams.DistAtten = _attenuator.Calculate(dist);
            }
#else
            newParams.DistAtten = _attenuator.Calculate(dist);
#endif

            newParams.Hrtf = SteamAudioManager.CurrentHRTF?.Get() ?? IntPtr.Zero;

            EnterParamsLock();
            _currentParams = newParams;
            ExitParamsLock();

            if (_bufferOverflowed)
            {
                _bufferOverflowed = false;
                Debug.LogError("Phonon Audio Buffer Size Overflow Exception.");
            }
        }

        private unsafe void OnAudioFilterRead(float[] data, int channels)
        {
            if (isBypass || channels != 2) return;

            DSPParams p = default;

            EnterParamsLock();
            p = _currentParams;
            ExitParamsLock();

            if (!Monitor.TryEnter(_lock))
                return;

            try
            {
                if (_binaural == IntPtr.Zero) return;

                int n = data.Length / 2;

                if (n > _monoIn.Length)
                {
                    _bufferOverflowed = true;
                    return;
                }

                float blend = Mathf.Clamp01(p.SpatialBlend);
                float effectiveAtten = Mathf.Lerp(1f, p.DistAtten, blend);

                fixed (float* pData = data)
                fixed (float* pIn = _monoIn, pOut = _monoOut, pLeft = _leftOut, pRight = _rightOut)
                {
                    // interleave to mono
                    for (int i = 0; i < n; i++)
                    {
                        pIn[i] = (pData[i * 2] + pData[i * 2 + 1]) * 0.5f * effectiveAtten;
                    }

                    IntPtr* inPtrs = stackalloc IntPtr[1]; inPtrs[0] = (IntPtr)pIn;
                    IntPtr* outPtrs = stackalloc IntPtr[1]; outPtrs[0] = (IntPtr)pOut;
                    IntPtr* binPtrs = stackalloc IntPtr[2]; binPtrs[0] = (IntPtr)pLeft; binPtrs[1] = (IntPtr)pRight;

                    var inBuf = new PAudioBuffer { numChannels = 1, numSamples = n, data = (IntPtr)inPtrs };
                    var outBuf = new PAudioBuffer { numChannels = 1, numSamples = n, data = (IntPtr)outPtrs };
                    var binBuf = new PAudioBuffer { numChannels = 2, numSamples = n, data = (IntPtr)binPtrs };

                    if (_direct != IntPtr.Zero)
                    {
                        int dflags = (int)DirectEffectFlags.ApplyOcclusion;
                        if (p.ApplyTransmission) dflags |= (int)DirectEffectFlags.ApplyTransmission;

                        var dp = new PDirectEffectParams
                        {
                            flags = dflags,
                            transmissionType = 1,
                            distanceAttenuation = 1f,
                            airAbsorptionLow = 1f,
                            airAbsorptionMid = 1f,
                            airAbsorptionHigh = 1f,
                            directivity = 1f,
                            occlusion = p.Occlusion,
                            transmissionLow = p.TransLow,
                            transmissionMid = p.TransMid,
                            transmissionHigh = p.TransHigh,
                        };
                        PhononNative.iplDirectEffectApply(_direct, ref dp, ref inBuf, ref outBuf);
                    }
                    else
                    {
                        for (int i = 0; i < n; i++) pOut[i] = pIn[i];
                    }

                    if (p.Hrtf != IntPtr.Zero)
                    {
                        var bp = new PBinauralEffectParams
                        {
                            direction = p.Dir,
                            interpolation = 1,
                            spatialBlend = blend,
                            hrtf = p.Hrtf,
                            peakDelays = IntPtr.Zero,
                        };
                        PhononNative.iplBinauralEffectApply(_binaural, ref bp, ref outBuf, ref binBuf);
                    }
                    else
                    {
                        for (int i = 0; i < n; i++) { pLeft[i] = pOut[i]; pRight[i] = pOut[i]; }
                    }

                    bool doReflections = p.HasValidReflData && _reflBufsAllocated && _reflectionEffect != IntPtr.Zero &&
                                         _ambiDecodeEffect != IntPtr.Zero && p.Hrtf != IntPtr.Zero && n == _nativeFrameSize &&
                                         _reflAmbiNative.data != IntPtr.Zero && _reflStereoNative.data != IntPtr.Zero;

                    if (doReflections)
                    {
                        var reflP = p.ReflParams;
                        PhononNative.iplReflectionEffectApply(_reflectionEffect, ref reflP, ref inBuf, ref _reflAmbiNative, IntPtr.Zero);

                        var ambiP = new PAmbisonicsDecodeEffectParams
                        {
                            order = _cachedMaxOrder,
                            hrtf = p.Hrtf,
                            orientation = p.ListenerCS,
                            binaural = 1,
                        };
                        PhononNative.iplAmbisonicsDecodeEffectApply(_ambiDecodeEffect, ref ambiP, ref _reflAmbiNative, ref _reflStereoNative);

                        float** reflChPtrs = (float**)_reflStereoNative.data.ToPointer();
                        float* reflL = reflChPtrs[0];
                        float* reflR = reflChPtrs[1];
                        float mix = p.ReflMixLevel;

                        for (int i = 0; i < n; i++)
                        {
                            pLeft[i] += reflL[i] * mix;
                            pRight[i] += reflR[i] * mix;
                        }
                    }

                    // write out back to Unity buffer vectorization loop
                    for (int i = 0; i < n; i++)
                    {
                        pData[i * 2] = pLeft[i];
                        pData[i * 2 + 1] = pRight[i];
                    }
                }
            }
            finally
            {
                Monitor.Exit(_lock);
            }
        }
    }
}
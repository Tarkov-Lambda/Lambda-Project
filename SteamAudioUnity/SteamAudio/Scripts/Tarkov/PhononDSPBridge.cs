using System;
using System.Runtime.InteropServices;
using UnityEngine;

#if STEAMAUDIO_ENABLED
using SteamAudio;
#endif

namespace ifp.arena.shared
{
    [RequireComponent(typeof(AudioSource))]
    public class PhononDSPBridge : MonoBehaviour
    {
        // ── phonon P/Invokes (phonon.dll) ─────────────────────────────────────

        private const string PHONON = "phonon";

        [DllImport(PHONON, CallingConvention = CallingConvention.Cdecl)]
        static extern int iplContextCreate(ref PCtxSettings s, out IntPtr ctx);
        [DllImport(PHONON, CallingConvention = CallingConvention.Cdecl)]
        static extern void iplContextRelease(ref IntPtr ctx);

        [DllImport(PHONON, CallingConvention = CallingConvention.Cdecl)]
        static extern int iplHRTFCreate(IntPtr ctx, ref PAudioSettings audio, ref PHrtfSettings hrtf, out IntPtr hrtfOut);
        [DllImport(PHONON, CallingConvention = CallingConvention.Cdecl)]
        static extern void iplHRTFRelease(ref IntPtr hrtf);

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

        // ── Static shared resources (context + HRTF, one per process) ────────

        private static IntPtr     s_ctx       = IntPtr.Zero;
        private static IntPtr     s_hrtf      = IntPtr.Zero;
        private static int        s_refCount  = 0;
        private static readonly object s_lock = new object();

        // ── Per-instance effects ──────────────────────────────────────────────

        private IntPtr _binaural = IntPtr.Zero;
        private IntPtr _direct   = IntPtr.Zero;

        // ── Unity components ──────────────────────────────────────────────────

        private AudioSource _src;
#if STEAMAUDIO_ENABLED
        private SteamAudioSource _steamSrc;
#endif

        // ── Audio settings ────────────────────────────────────────────────────

        private int _rate;
        private int _bufSize;

        // ── Audio-thread cache ────────────────────────────────────────────────

        private PVec3 _dir               = new PVec3 { z = 1f };
        private float _distAtten         = 1f;
        private float _occlusion         = 1f;
        private float _transLow          = 1f;
        private float _transMid          = 1f;
        private float _transHigh         = 1f;
        private readonly object _lock    = new object();

        // ── Pre-allocated DSP scratch buffers ─────────────────────────────────

        private float[] _monoIn;
        private float[] _monoOut;
        private float[] _leftOut;
        private float[] _rightOut;

        // ── Logging control ───────────────────────────────────────────────────

        /// <summary>Enable extremely verbose per-frame and per-buffer logging. 
        /// WARNING: This will flood the console. Use only for short debugging sessions.</summary>
        [Header("Debug Logging")]
        [Tooltip("Master toggle for all verbose logging from this component.")]
        public bool verboseLogging = true;

        [Tooltip("Log per-audio-frame DSP details (very spammy — runs on audio thread).")]
        public bool logAudioThread = false;

        [Tooltip("Log per-Update spatial / simulation values every frame.")]
        public bool logUpdateValues = true;

        [Tooltip("Throttle Update logging to once every N frames (0 = every frame).")]
        public int logUpdateEveryNFrames = 60;

        [Tooltip("Log lifecycle events (Awake, Destroy, Init).")]
        public bool logLifecycle = true;

        [Tooltip("Log audio thread buffer statistics (RMS, peak, silence detection).")]
        public bool logBufferStats = false;

        [Tooltip("Throttle audio thread logging to once every N callbacks (0 = every callback).")]
        public int logAudioEveryNCallbacks = 100;

        private int _updateFrameCounter = 0;
        private int _audioCallbackCounter = 0;
        private int _totalAudioCallbacks = 0;
        private int _silentBufferCount = 0;
        private int _nonSilentBufferCount = 0;
        private bool _initSucceeded = false;
        private string _instanceId;

        // ─────────────────────────────────────────────────────────────────────
        //  Logging helpers (avoid string alloc when logging is off)
        // ─────────────────────────────────────────────────────────────────────

        private void LogV(string msg)
        {
            if (verboseLogging) Debug.Log($"[PhononDSPBridge:{_instanceId}] {msg}");
        }

        private void LogW(string msg)
        {
            Debug.LogWarning($"[PhononDSPBridge:{_instanceId}] {msg}");
        }

        private void LogE(string msg)
        {
            Debug.LogError($"[PhononDSPBridge:{_instanceId}] {msg}");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            _instanceId = $"{gameObject.name}_{GetInstanceID()}";

            if (logLifecycle) LogV("Awake() BEGIN");

            _src = GetComponent<AudioSource>();
            if (_src == null)
            {
                LogE("No AudioSource found on this GameObject!");
                return;
            }

            if (logLifecycle)
            {
                LogV($"  AudioSource found: clip={((_src.clip != null) ? _src.clip.name : "NULL")}, " +
                     $"playing={_src.isPlaying}, spatialize={_src.spatialize}, spatialBlend={_src.spatialBlend}, " +
                     $"volume={_src.volume}, mute={_src.mute}, loop={_src.loop}, " +
                     $"minDistance={_src.minDistance}, maxDistance={_src.maxDistance}, " +
                     $"rolloffMode={_src.rolloffMode}, outputAudioMixerGroup={_src.outputAudioMixerGroup}");
            }

#if STEAMAUDIO_ENABLED
            _steamSrc = GetComponent<SteamAudioSource>();
            if (logLifecycle) LogV($"  SteamAudioSource found: {(_steamSrc != null ? "YES" : "NO")}");
#else
            if (logLifecycle) LogV("  STEAMAUDIO_ENABLED is NOT defined — SteamAudioSource integration disabled");
#endif

            _src.spatialize   = false;
            _src.spatialBlend = 0f;
            if (logLifecycle) LogV("  Set spatialize=false, spatialBlend=0");

            _rate    = UnityEngine.AudioSettings.outputSampleRate;
            UnityEngine.AudioSettings.GetDSPBufferSize(out _bufSize, out int numBufs);
            if (logLifecycle) LogV($"  Audio config: sampleRate={_rate}, dspBufferSize={_bufSize}, numBuffers={numBufs}");

            int cap = _bufSize * 2;
            _monoIn   = new float[cap];
            _monoOut  = new float[cap];
            _leftOut  = new float[cap];
            _rightOut = new float[cap];
            if (logLifecycle) LogV($"  Allocated scratch buffers: capacity={cap} floats each");

            InitPhonon();

            if (logLifecycle) LogV($"Awake() END — initSucceeded={_initSucceeded}");
        }

        private void OnEnable()
        {
            if (logLifecycle) LogV($"OnEnable() — binaural={_binaural != IntPtr.Zero}, direct={_direct != IntPtr.Zero}");
        }

        private void OnDisable()
        {
            if (logLifecycle) LogV($"OnDisable() — totalAudioCallbacks={_totalAudioCallbacks}, " +
                                   $"silentBuffers={_silentBufferCount}, nonSilentBuffers={_nonSilentBufferCount}");
        }

        private void OnDestroy()
        {
            if (logLifecycle) LogV("OnDestroy() BEGIN");

            lock (_lock)
            {
                if (_binaural != IntPtr.Zero)
                {
                    if (logLifecycle) LogV("  Releasing binaural effect...");
                    iplBinauralEffectRelease(ref _binaural);
                    _binaural = IntPtr.Zero;
                }
                if (_direct != IntPtr.Zero)
                {
                    if (logLifecycle) LogV("  Releasing direct effect...");
                    iplDirectEffectRelease(ref _direct);
                    _direct = IntPtr.Zero;
                }
            }

            lock (s_lock)
            {
                s_refCount--;
                if (logLifecycle) LogV($"  s_refCount decremented to {s_refCount}");

                if (s_refCount <= 0)
                {
                    s_refCount = 0;
                    if (s_hrtf != IntPtr.Zero)
                    {
                        if (logLifecycle) LogV("  Releasing shared HRTF...");
                        iplHRTFRelease(ref s_hrtf);
                        s_hrtf = IntPtr.Zero;
                    }
                    if (s_ctx != IntPtr.Zero)
                    {
                        if (logLifecycle) LogV("  Releasing shared context...");
                        iplContextRelease(ref s_ctx);
                        s_ctx = IntPtr.Zero;
                    }
                }
            }

            if (logLifecycle) LogV($"OnDestroy() END — total audio callbacks processed: {_totalAudioCallbacks}");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Phonon initialisation
        // ─────────────────────────────────────────────────────────────────────

        private void InitPhonon()
        {
            if (logLifecycle) LogV("InitPhonon() BEGIN");

            lock (s_lock)
            {
                if (s_ctx == IntPtr.Zero)
                {
                    var ctxS = new PCtxSettings { version = 0x00040801 };
                    if (logLifecycle) LogV($"  Creating Phonon context with version=0x{ctxS.version:X8}...");

                    int r = iplContextCreate(ref ctxS, out s_ctx);
                    if (r != 0 || s_ctx == IntPtr.Zero)
                    {
                        LogE($"iplContextCreate FAILED (returnCode={r}, ptr={(s_ctx == IntPtr.Zero ? "NULL" : s_ctx.ToString())})");
                        _initSucceeded = false;
                        return;
                    }
                    if (logLifecycle) LogV($"  iplContextCreate SUCCESS (ptr=0x{s_ctx.ToInt64():X}, returnCode={r})");
                }
                else
                {
                    if (logLifecycle) LogV($"  Reusing existing Phonon context (ptr=0x{s_ctx.ToInt64():X}, refCount={s_refCount})");
                }

                if (s_hrtf == IntPtr.Zero)
                {
                    var a = new PAudioSettings { samplingRate = _rate, frameSize = _bufSize };
                    var h = new PHrtfSettings  { type = 0, volume = 1f };
                    if (logLifecycle) LogV($"  Creating HRTF: type=Default, volume={h.volume}, sampleRate={a.samplingRate}, frameSize={a.frameSize}...");

                    int r = iplHRTFCreate(s_ctx, ref a, ref h, out s_hrtf);
                    if (r != 0 || s_hrtf == IntPtr.Zero)
                    {
                        LogE($"iplHRTFCreate FAILED (returnCode={r}, ptr={(s_hrtf == IntPtr.Zero ? "NULL" : s_hrtf.ToString())})");
                        _initSucceeded = false;
                        return;
                    }
                    if (logLifecycle) LogV($"  iplHRTFCreate SUCCESS (ptr=0x{s_hrtf.ToInt64():X}, returnCode={r})");
                }
                else
                {
                    if (logLifecycle) LogV($"  Reusing existing HRTF (ptr=0x{s_hrtf.ToInt64():X})");
                }

                s_refCount++;
                if (logLifecycle) LogV($"  s_refCount incremented to {s_refCount}");
            }

            var audio = new PAudioSettings { samplingRate = _rate, frameSize = _bufSize };

            // Binaural effect
            {
                var binS = new PBinauralEffectSettings { hrtf = s_hrtf };
                if (logLifecycle) LogV($"  Creating binaural effect: hrtf=0x{binS.hrtf.ToInt64():X}, sampleRate={audio.samplingRate}, frameSize={audio.frameSize}...");

                int r = iplBinauralEffectCreate(s_ctx, ref audio, ref binS, out _binaural);
                if (r != 0 || _binaural == IntPtr.Zero)
                {
                    LogE($"iplBinauralEffectCreate FAILED (returnCode={r}, ptr={(_binaural == IntPtr.Zero ? "NULL" : _binaural.ToString())})");
                    _initSucceeded = false;
                    return;
                }
                if (logLifecycle) LogV($"  iplBinauralEffectCreate SUCCESS (ptr=0x{_binaural.ToInt64():X}, returnCode={r})");
            }

            // Direct effect
            {
                var dirS = new PDirectEffectSettings { numChannels = 1 };
                if (logLifecycle) LogV($"  Creating direct effect: numChannels={dirS.numChannels}...");

                int r = iplDirectEffectCreate(s_ctx, ref audio, ref dirS, out _direct);
                if (r != 0 || _direct == IntPtr.Zero)
                {
                    LogE($"iplDirectEffectCreate FAILED (returnCode={r}, ptr={(_direct == IntPtr.Zero ? "NULL" : _direct.ToString())})");
                    // Not fatal — we can still do binaural without direct effect
                    LogW("Direct effect unavailable — will bypass occlusion/transmission processing");
                }
                else
                {
                    if (logLifecycle) LogV($"  iplDirectEffectCreate SUCCESS (ptr=0x{_direct.ToInt64():X}, returnCode={r})");
                }
            }

            _initSucceeded = true;
            if (logLifecycle) LogV("InitPhonon() END — all effects created successfully");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Per-frame: compute direction, read simulation outputs
        // ─────────────────────────────────────────────────────────────────────

        private void Update()
        {
            _updateFrameCounter++;

            Transform listener = GetListener();
            if (listener == null)
            {
                if (logUpdateValues && (_updateFrameCounter % Mathf.Max(1, logUpdateEveryNFrames)) == 0)
                    LogW("Update(): No listener found (Camera.main and AudioListener both null)!");
                return;
            }

            UnityEngine.Vector3 worldDir = (transform.position - listener.position);
            float dist = worldDir.magnitude;
            UnityEngine.Vector3 d = listener.InverseTransformPoint(transform.position).normalized;

            float maxDist = (_src != null && _src.maxDistance > 0f) ? _src.maxDistance : 50f;
            float atten   = Mathf.Clamp01(1f - dist / maxDist);

            float occ = 1f, tLow = 1f, tMid = 1f, tHigh = 1f;
#if STEAMAUDIO_ENABLED
            if (_steamSrc != null)
            {
                occ   = Mathf.Clamp01(_steamSrc.occlusionValue);
                tLow  = Mathf.Clamp01(_steamSrc.transmissionLow);
                tMid  = Mathf.Clamp01(_steamSrc.transmissionMid);
                tHigh = Mathf.Clamp01(_steamSrc.transmissionHigh);
            }
#endif

            bool shouldLog = logUpdateValues &&
                             (logUpdateEveryNFrames <= 0 || (_updateFrameCounter % logUpdateEveryNFrames) == 0);

            if (shouldLog)
            {
                LogV($"Update() frame={_updateFrameCounter}: " +
                     $"srcPos={transform.position}, listenerPos={listener.position}, " +
                     $"dist={dist:F3}, maxDist={maxDist:F1}, atten={atten:F4}, " +
                     $"localDir=({d.x:F3},{d.y:F3},{d.z:F3}), " +
                     $"phononDir=({d.x:F3},{d.y:F3},{-d.z:F3}), " +
                     $"occ={occ:F3}, tLow={tLow:F3}, tMid={tMid:F3}, tHigh={tHigh:F3}" +
#if STEAMAUDIO_ENABLED
                     $", steamSrc={(_steamSrc != null ? "present" : "NULL")}" +
#else
                     $", steamSrc=N/A(disabled)" +
#endif
                     $", audioSrc.isPlaying={(_src != null ? _src.isPlaying.ToString() : "N/A")}" +
                     $", audioSrc.volume={(_src != null ? _src.volume.ToString("F3") : "N/A")}" +
                     $", audioSrc.mute={(_src != null ? _src.mute.ToString() : "N/A")}");
            }

            lock (_lock)
            {
                _dir       = new PVec3 { x = d.x, y = d.y, z = -d.z };
                _distAtten = atten;
                _occlusion = occ;
                _transLow  = tLow;
                _transMid  = tMid;
                _transHigh = tHigh;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  OnAudioFilterRead – runs on Unity's audio thread
        // ─────────────────────────────────────────────────────────────────────

        private unsafe void OnAudioFilterRead(float[] data, int channels)
        {
            _totalAudioCallbacks++;
            _audioCallbackCounter++;

            bool shouldLogThisCallback = (logAudioThread || logBufferStats) &&
                                         (logAudioEveryNCallbacks <= 0 ||
                                          (_audioCallbackCounter % logAudioEveryNCallbacks) == 0);

            if (channels != 2)
            {
                if (shouldLogThisCallback)
                    LogW($"OnAudioFilterRead: SKIPPING — unexpected channel count={channels} (expected 2)");
                return;
            }

            if (_binaural == IntPtr.Zero)
            {
                if (shouldLogThisCallback)
                    LogW($"OnAudioFilterRead: SKIPPING — _binaural is NULL (init may have failed, initSucceeded={_initSucceeded})");
                return;
            }

            lock (_lock)
            {
                int n = data.Length / channels;

                // ── Incoming buffer stats ─────────────────────────────────────
                float inRms = 0f, inPeak = 0f;
                if (shouldLogThisCallback && logBufferStats)
                {
                    for (int i = 0; i < data.Length; i++)
                    {
                        float abs = Mathf.Abs(data[i]);
                        inRms += data[i] * data[i];
                        if (abs > inPeak) inPeak = abs;
                    }
                    inRms = Mathf.Sqrt(inRms / data.Length);
                }

                bool inputIsSilent = true;
                if (logBufferStats)
                {
                    for (int i = 0; i < data.Length && inputIsSilent; i++)
                    {
                        if (Mathf.Abs(data[i]) > 1e-8f) inputIsSilent = false;
                    }
                    if (inputIsSilent) _silentBufferCount++;
                    else _nonSilentBufferCount++;
                }

                if (shouldLogThisCallback && logAudioThread)
                {
                    LogV($"OnAudioFilterRead #{_totalAudioCallbacks}: " +
                         $"dataLen={data.Length}, channels={channels}, samples={n}, " +
                         $"bufferCapacity={_monoIn.Length}, " +
                         $"binaural=0x{_binaural.ToInt64():X}, direct=0x{(_direct != IntPtr.Zero ? _direct.ToInt64() : 0):X}, " +
                         $"distAtten={_distAtten:F4}, dir=({_dir.x:F3},{_dir.y:F3},{_dir.z:F3}), " +
                         $"occ={_occlusion:F3}, tL={_transLow:F3}, tM={_transMid:F3}, tH={_transHigh:F3}");
                }

                if (shouldLogThisCallback && logBufferStats)
                {
                    LogV($"  INPUT buffer stats: RMS={inRms:F6}, peak={inPeak:F6}, " +
                         $"silent={inputIsSilent}, silentTotal={_silentBufferCount}, nonSilentTotal={_nonSilentBufferCount}");
                }

                // Grow scratch buffers if needed
                if (n > _monoIn.Length)
                {
                    LogW($"OnAudioFilterRead: Scratch buffer RESIZE needed! n={n} > capacity={_monoIn.Length}. " +
                         $"This should not normally happen after Awake().");
                    _monoIn   = new float[n];
                    _monoOut  = new float[n];
                    _leftOut  = new float[n];
                    _rightOut = new float[n];
                }

                // ── 1. Downmix to mono + distance attenuation ─────────────────
                float monoRms = 0f, monoPeak = 0f;
                for (int i = 0; i < n; i++)
                {
                    _monoIn[i] = (data[i * channels] + data[i * channels + 1]) * 0.5f * _distAtten;
                    if (logBufferStats && shouldLogThisCallback)
                    {
                        float abs = Mathf.Abs(_monoIn[i]);
                        monoRms += _monoIn[i] * _monoIn[i];
                        if (abs > monoPeak) monoPeak = abs;
                    }
                }

                if (shouldLogThisCallback && logBufferStats)
                {
                    monoRms = Mathf.Sqrt(monoRms / Mathf.Max(1, n));
                    LogV($"  MONO downmix stats: RMS={monoRms:F6}, peak={monoPeak:F6}, distAtten={_distAtten:F4}");
                }

                fixed (float* pIn   = _monoIn,
                              pOut  = _monoOut,
                              pLeft = _leftOut,
                              pRight= _rightOut)
                {
                    IntPtr* inPtrs  = stackalloc IntPtr[1];  inPtrs[0]  = (IntPtr)pIn;
                    IntPtr* outPtrs = stackalloc IntPtr[1];  outPtrs[0] = (IntPtr)pOut;
                    IntPtr* binPtrs = stackalloc IntPtr[2];  binPtrs[0] = (IntPtr)pLeft; binPtrs[1] = (IntPtr)pRight;

                    var inBuf  = new PAudioBuffer { numChannels = 1, numSamples = n, data = (IntPtr)inPtrs  };
                    var outBuf = new PAudioBuffer { numChannels = 1, numSamples = n, data = (IntPtr)outPtrs };
                    var binBuf = new PAudioBuffer { numChannels = 2, numSamples = n, data = (IntPtr)binPtrs };

                    // ── 2. Direct effect: occlusion + transmission ────────────
                    if (_direct != IntPtr.Zero)
                    {
                        var dp = new PDirectEffectParams
                        {
                            flags            = (1 << 3) | (1 << 4),
                            transmissionType = 1,
                            distanceAttenuation = 1f,
                            airAbsorptionLow = 1f, airAbsorptionMid = 1f, airAbsorptionHigh = 1f,
                            directivity      = 1f,
                            occlusion        = _occlusion,
                            transmissionLow  = _transLow,
                            transmissionMid  = _transMid,
                            transmissionHigh = _transHigh,
                        };

                        if (shouldLogThisCallback && logAudioThread)
                        {
                            LogV($"  DirectEffect APPLY: flags=0x{dp.flags:X}, transmissionType={dp.transmissionType}, " +
                                 $"distAtten={dp.distanceAttenuation:F3}, occ={dp.occlusion:F3}, " +
                                 $"tL={dp.transmissionLow:F3}, tM={dp.transmissionMid:F3}, tH={dp.transmissionHigh:F3}, " +
                                 $"inBuf(ch={inBuf.numChannels},n={inBuf.numSamples}), " +
                                 $"outBuf(ch={outBuf.numChannels},n={outBuf.numSamples})");
                        }

                        int dirResult = iplDirectEffectApply(_direct, ref dp, ref inBuf, ref outBuf);

                        if (shouldLogThisCallback && logAudioThread)
                        {
                            // Compute post-direct stats
                            float dirRms = 0f, dirPeak = 0f;
                            if (logBufferStats)
                            {
                                for (int i = 0; i < n; i++)
                                {
                                    float abs = Mathf.Abs(_monoOut[i]);
                                    dirRms += _monoOut[i] * _monoOut[i];
                                    if (abs > dirPeak) dirPeak = abs;
                                }
                                dirRms = Mathf.Sqrt(dirRms / Mathf.Max(1, n));
                            }

                            LogV($"  DirectEffect result={dirResult} (0=OK). " +
                                 (logBufferStats ? $"Post-direct: RMS={dirRms:F6}, peak={dirPeak:F6}" : ""));
                        }
                    }
                    else
                    {
                        if (shouldLogThisCallback && logAudioThread)
                            LogV("  DirectEffect SKIPPED (_direct is NULL) — passthrough mono");

                        for (int i = 0; i < n; i++) _monoOut[i] = _monoIn[i];
                    }

                    // ── 3. Binaural effect: HRTF spatialization ───────────────
                    var bp = new PBinauralEffectParams
                    {
                        direction    = _dir,
                        interpolation = 1,
                        spatialBlend = 1f,
                        hrtf         = s_hrtf,
                        peakDelays   = IntPtr.Zero,
                    };

                    if (shouldLogThisCallback && logAudioThread)
                    {
                        LogV($"  BinauralEffect APPLY: dir=({bp.direction.x:F3},{bp.direction.y:F3},{bp.direction.z:F3}), " +
                             $"interp={bp.interpolation}, spatialBlend={bp.spatialBlend:F2}, " +
                             $"hrtf=0x{bp.hrtf.ToInt64():X}, " +
                             $"inBuf(ch={outBuf.numChannels},n={outBuf.numSamples}), " +
                             $"outBuf(ch={binBuf.numChannels},n={binBuf.numSamples})");
                    }

                    int binResult = iplBinauralEffectApply(_binaural, ref bp, ref outBuf, ref binBuf);

                    if (shouldLogThisCallback && logAudioThread)
                    {
                        float lRms = 0f, rRms = 0f, lPeak = 0f, rPeak = 0f;
                        if (logBufferStats)
                        {
                            for (int i = 0; i < n; i++)
                            {
                                float la = Mathf.Abs(_leftOut[i]);
                                float ra = Mathf.Abs(_rightOut[i]);
                                lRms += _leftOut[i] * _leftOut[i];
                                rRms += _rightOut[i] * _rightOut[i];
                                if (la > lPeak) lPeak = la;
                                if (ra > rPeak) rPeak = ra;
                            }
                            lRms = Mathf.Sqrt(lRms / Mathf.Max(1, n));
                            rRms = Mathf.Sqrt(rRms / Mathf.Max(1, n));
                        }

                        LogV($"  BinauralEffect result={binResult} (0=OK, 1=OutputSilent). " +
                             (logBufferStats ? $"L: RMS={lRms:F6} peak={lPeak:F6}, R: RMS={rRms:F6} peak={rPeak:F6}" : ""));
                    }
                }

                // ── 4. Write stereo binaural result into Unity's buffer ───────
                for (int i = 0; i < n; i++)
                {
                    data[i * channels]     = _leftOut[i];
                    data[i * channels + 1] = _rightOut[i];
                }

                // ── Final output stats ────────────────────────────────────────
                if (shouldLogThisCallback && logBufferStats)
                {
                    float outRms = 0f, outPeak = 0f;
                    for (int i = 0; i < data.Length; i++)
                    {
                        float abs = Mathf.Abs(data[i]);
                        outRms += data[i] * data[i];
                        if (abs > outPeak) outPeak = abs;
                    }
                    outRms = Mathf.Sqrt(outRms / data.Length);

                    bool outputIsSilent = outPeak < 1e-8f;
                    LogV($"  FINAL OUTPUT stats: RMS={outRms:F6}, peak={outPeak:F6}, silent={outputIsSilent}");

                    if (!inputIsSilent && outputIsSilent)
                        LogW("  ⚠ Non-silent input produced SILENT output! Check attenuation, direction, or effect parameters.");
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Helpers
        // ─────────────────────────────────────────────────────────────────────

        private Transform GetListener()
        {
            var cam = Camera.main;
            if (cam != null) return cam.transform;
            var al = FindObjectOfType<AudioListener>();
            return al != null ? al.transform : null;
        }

        // ── Inspector summary ─────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (verboseLogging)
            {
                Debug.Log($"[PhononDSPBridge] Logging settings changed on {gameObject.name}: " +
                          $"verbose={verboseLogging}, audioThread={logAudioThread}, " +
                          $"updateValues={logUpdateValues} (every {logUpdateEveryNFrames}f), " +
                          $"lifecycle={logLifecycle}, bufferStats={logBufferStats}, " +
                          $"audioEveryN={logAudioEveryNCallbacks}");
            }
        }
#endif
    }

    // ── Minimal phonon struct definitions (mirrors phonon.h layout exactly) ──

    [StructLayout(LayoutKind.Sequential)]
    internal struct PCtxSettings
    {
        public uint   version;
        public IntPtr logCallback;
        public IntPtr allocateCallback;
        public IntPtr freeCallback;
        public int    simdLevel;
        public int    flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PAudioSettings { public int samplingRate; public int frameSize; }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PVec3 { public float x, y, z; }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PHrtfSettings
    {
        public int    type;
        public IntPtr sofaFileName;
        public IntPtr sofaData;
        public int    sofaDataSize;
        public float  volume;
        public int    normType;
    }

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
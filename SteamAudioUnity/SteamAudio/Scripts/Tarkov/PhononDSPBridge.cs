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

        private IntPtr _binaural = IntPtr.Zero;
        private IntPtr _direct = IntPtr.Zero;

        private AudioSource _src;
#if STEAMAUDIO_ENABLED
        private SteamAudioSource _steamSrc;
#endif

        private PVec3 _dir = new PVec3 { z = 1f };
        private float _distAtten = 1f;
        private float _occlusion = 1f;
        private float _transLow = 0f;
        private float _transMid = 0f;
        private float _transHigh = 0f;

        private IntPtr _hrtf = IntPtr.Zero;
        private bool _applyTransmission = false;

        private readonly object _lock = new object();

        private float[] _monoIn;
        private float[] _monoOut;
        private float[] _leftOut;
        private float[] _rightOut;

        [Header("Spatial Blend")]
        [Range(0f, 1f)]
        public float spatialBlendOverride = 1f;

        [Header("Debug")]
        public bool verboseLogging = false;

        [Header("Bypass")]
        public bool IsBypass = false;

        private string _instanceId;

        // --- ADDED CACHE VARIABLES FOR DISTANCE ATTENUATION ---
        private AnimationCurve _cachedRolloffCurve;
        private AudioRolloffMode _lastRolloffMode = (AudioRolloffMode)(-1);

        private void LogV(string msg) { if (verboseLogging) Debug.Log($"[PhononDSPBridge:{_instanceId}] {msg}"); }
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

            InitEffects();
        }

        private void OnDestroy()
        {
            lock (_lock)
            {
                if (_binaural != IntPtr.Zero) { iplBinauralEffectRelease(ref _binaural); _binaural = IntPtr.Zero; }
                if (_direct != IntPtr.Zero) { iplDirectEffectRelease(ref _direct); _direct = IntPtr.Zero; }
            }
        }

        private void InitEffects()
        {
            if (SteamAudioManager.Singleton == null || SteamAudioManager.Context == null) return;

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
            rate      = UnityEngine.AudioSettings.outputSampleRate;
            UnityEngine.AudioSettings.GetDSPBufferSize(out frameSize, out _);
#endif
            var audio = new PAudioSettings { samplingRate = rate, frameSize = frameSize };

            var binS = new PBinauralEffectSettings { hrtf = hrtf };
            int r = iplBinauralEffectCreate(ctx, ref audio, ref binS, out _binaural);
            if (r != 0 || _binaural == IntPtr.Zero) return;

            var dirS = new PDirectEffectSettings { numChannels = 1 };
            iplDirectEffectCreate(ctx, ref audio, ref dirS, out _direct);
        }


        private float _lastMaxDist = -1f;


private float CalculateDistanceAttenuation(float dist)
        {
            if (_src == null) return 0f;

            float minDist = _src.minDistance;
            float maxDist = Mathf.Max(_src.maxDistance, minDist + 0.001f);

            // --- DEBUG ---
            LogV($"  Calculating Attenuation for distance: {dist:F2}m (Min: {minDist:F1}, Max: {maxDist:F1})");
            LogV($"  AudioSource RolloffMode is: {_src.rolloffMode}");

            if (dist <= minDist) return 1f;
            if (dist >= maxDist) return 0f;

            if (_src.rolloffMode == AudioRolloffMode.Custom)
            {
                if (_cachedRolloffCurve == null || _lastMaxDist != maxDist)
                {
                    // --- DEBUG ---
                    LogV("  -> Recaching custom rolloff curve...");
                    _cachedRolloffCurve = _src.GetCustomCurve(AudioSourceCurveType.CustomRolloff);
                    _lastMaxDist = maxDist;

                    if (_cachedRolloffCurve == null || _cachedRolloffCurve.length == 0)
                    {
                        LogW("  -> Custom curve is NULL or empty! Falling back to Linear.");
                        return 1f - ((dist - minDist) / (maxDist - minDist));
                    }
                }

                float normalizedDist = dist / maxDist;
                float result = Mathf.Clamp01(_cachedRolloffCurve.Evaluate(normalizedDist));
                
                // --- DEBUG ---
                LogV($"  -> Custom Curve evaluation: Evaluate({normalizedDist:F3}) => {result:F3}");
                return result;
            }

            if (_src.rolloffMode == AudioRolloffMode.Linear)
            {
                float result = 1f - ((dist - minDist) / (maxDist - minDist));
                // --- DEBUG ---
                LogV($"  -> Linear evaluation result: {result:F3}");
                return result;
            }
            
            if (_src.rolloffMode == AudioRolloffMode.Logarithmic)
            {
                float result = minDist / dist;
                 // --- DEBUG ---
                LogV($"  -> Logarithmic evaluation result: {result:F3}");
                return result;
            }

            LogW("  -> No valid rolloff mode found! Attenuation will be 1.0 (no effect).");
            return 1f;
        }

        private void Update()
        {
            Transform listener = GetListenerTransform();
            if (listener == null) return;

            UnityEngine.Vector3 localPos = listener.InverseTransformPoint(transform.position);
            UnityEngine.Vector3 d = localPos.sqrMagnitude < 0.000001f ? UnityEngine.Vector3.forward : localPos.normalized;

            float dist = (transform.position - listener.position).magnitude;

            // Replaced the buggy 1f - dist / maxDist with correct engine emulation
            float atten = CalculateDistanceAttenuation(dist);

            float occ = 1f, tLow = 0f, tMid = 0f, tHigh = 0f;
            bool applyTrans = false;

#if STEAMAUDIO_ENABLED
            if (_steamSrc != null)
            {
                occ = Mathf.Clamp01(_steamSrc.occlusionValue);
                applyTrans = _steamSrc.transmission;
                if (_steamSrc.transmission)
                {
                    tLow = Mathf.Clamp01(_steamSrc.transmissionLow);
                    tMid = Mathf.Clamp01(_steamSrc.transmissionMid);
                    tHigh = Mathf.Clamp01(_steamSrc.transmissionHigh);
                }
            }
#endif
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
            }
        }

        private unsafe void OnAudioFilterRead(float[] data, int channels)
        {
            if (IsBypass || channels != 2 || _binaural == IntPtr.Zero)
            {
                if (IsBypass) Array.Clear(data, 0, data.Length);
                return;
            }

            lock (_lock)
            {
                int n = data.Length / channels;

                if (n > _monoIn.Length)
                {
                    _monoIn = new float[n];
                    _monoOut = new float[n];
                    _leftOut = new float[n];
                    _rightOut = new float[n];
                }

                float blend = Mathf.Clamp01(spatialBlendOverride);
                // Note: Multiplying `_distAtten` manually like this is actually brilliant,
                // because native Phonon doesn't support interpolating distance rolloff using a Unity `spatialBlend` equivalent.
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

                    if (_direct != IntPtr.Zero)
                    {
                        int flags = (int)DirectEffectFlags.ApplyOcclusion;
                        if (_applyTransmission) flags |= (int)DirectEffectFlags.ApplyTransmission;

                        var dp = new PDirectEffectParams
                        {
                            flags = flags,
                            transmissionType = 1,
                            distanceAttenuation = 1f, // Safe to keep 1f since you pre-multiplied `effectiveAtten` on _monoIn loop
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
                        iplBinauralEffectApply(_binaural, ref bp, ref outBuf, ref binBuf);
                    }
                    else
                    {
                        for (int i = 0; i < n; i++)
                        {
                            _leftOut[i] = _monoOut[i];
                            _rightOut[i] = _monoOut[i];
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

    [StructLayout(LayoutKind.Sequential)]
    internal struct PAudioBuffer
    {
        public int numChannels;
        public int numSamples;
        public IntPtr data;
    }
}

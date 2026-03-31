using System;
using System.Runtime.InteropServices;
using UnityEngine;

public class DirectPhononSpatializer : MonoBehaviour
{
    const string PHONON_LIB = "phonon";
    
    [DllImport(PHONON_LIB, CallingConvention = CallingConvention.Cdecl)] 
    static extern int iplContextCreate(ref IPLContextSettings settings, out IntPtr context);
    
    [DllImport(PHONON_LIB, CallingConvention = CallingConvention.Cdecl)]
    static extern int iplHRTFCreate(IntPtr context, ref IPLAudioSettings audioSettings, 
        ref IPLHRTFSettings hrtfSettings, out IntPtr hrtf);
    
    [DllImport(PHONON_LIB, CallingConvention = CallingConvention.Cdecl)]
    static extern int iplBinauralEffectCreate(IntPtr context, ref IPLAudioSettings audioSettings, 
        ref IPLBinauralEffectSettings effectSettings, out IntPtr effect);
    
    [DllImport(PHONON_LIB, CallingConvention = CallingConvention.Cdecl)]
    static extern int iplBinauralEffectApply(IntPtr effect, ref IPLBinauralEffectParams params_, 
        ref IPLAudioBuffer inBuffer, ref IPLAudioBuffer outBuffer);

    [DllImport(PHONON_LIB, CallingConvention = CallingConvention.Cdecl)] 
    static extern void iplBinauralEffectRelease(ref IntPtr effect);
    
    [DllImport(PHONON_LIB, CallingConvention = CallingConvention.Cdecl)] 
    static extern void iplHRTFRelease(ref IntPtr hrtf);
    
    [DllImport(PHONON_LIB, CallingConvention = CallingConvention.Cdecl)] 
    static extern void iplContextRelease(ref IntPtr context);

    private IntPtr phononContext;
    private IntPtr hrtf;
    private IntPtr binauralEffect;
    private AudioSource audioSource;
    
    private int sampleRate;
    private int bufferSize;

    // Cached variables for the Audio Thread
    private IPLVector3 currentDirection;
    private float currentDistanceAttenuation = 1f;
    private readonly object audioLock = new object();

    // Pre-allocated buffers to prevent GC Spikes
    private float[] monoIn;
    private float[] leftOut;
    private float[] rightOut;

    private bool isRead = false;

    // Throttle audio-thread logs to avoid flooding (logs every N DSP callbacks)
    private int _audioCallbackCount = 0;
    private const int AUDIO_LOG_INTERVAL = 100;

    void Awake()
    {
        Debug.Log($"[DirectPhononSpatializer] Awake() called on GameObject: '{gameObject.name}' (instanceID={GetInstanceID()})");

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            Debug.LogError("[DirectPhononSpatializer] Awake() - NO AudioSource found on this GameObject! Spatialization will not work.");
        }
        else
        {
            Debug.Log($"[DirectPhononSpatializer] Awake() - AudioSource found. " +
                      $"clip={audioSource.clip?.name ?? "NULL"}, " +
                      $"isPlaying={audioSource.isPlaying}, " +
                      $"volume={audioSource.volume}, " +
                      $"mute={audioSource.mute}, " +
                      $"spatialize={audioSource.spatialize}, " +
                      $"spatialBlend={audioSource.spatialBlend}, " +
                      $"outputAudioMixerGroup={audioSource.outputAudioMixerGroup?.name ?? "NULL"}, " +
                      $"playOnAwake={audioSource.playOnAwake}, " +
                      $"loop={audioSource.loop}, " +
                      $"maxDistance={audioSource.maxDistance}");
        }

        audioSource.spatialize = false; 
        audioSource.spatialBlend = 0f;  
        Debug.Log("[DirectPhononSpatializer] Awake() - Forced AudioSource.spatialize=false, spatialBlend=0 (we do our own spatialization).");
        
        sampleRate = AudioSettings.outputSampleRate;
        AudioSettings.GetDSPBufferSize(out bufferSize, out int numBuffers);
        Debug.Log($"[DirectPhononSpatializer] Awake() - Audio system: sampleRate={sampleRate} Hz, bufferSize={bufferSize} samples, numBuffers={numBuffers}");
        Debug.Log($"[DirectPhononSpatializer] Awake() - AudioSettings.speakerMode={AudioSettings.speakerMode}");

        // Pre-allocate arrays for max expected size
        monoIn  = new float[bufferSize * 2];
        leftOut = new float[bufferSize * 2];
        rightOut= new float[bufferSize * 2];
        Debug.Log($"[DirectPhononSpatializer] Awake() - Pre-allocated mono/left/right buffers: {bufferSize * 2} floats each.");
        
        InitPhonon();

        Debug.Log($"[DirectPhononSpatializer] Awake() - After InitPhonon: " +
                  $"phononContext={phononContext}, hrtf={hrtf}, binauralEffect={binauralEffect}");
        if (phononContext == IntPtr.Zero)
            Debug.LogError("[DirectPhononSpatializer] Awake() - phononContext is NULL after init! All phonon calls will fail.");
        if (hrtf == IntPtr.Zero)
            Debug.LogError("[DirectPhononSpatializer] Awake() - hrtf is NULL after init! Binaural rendering will fail.");
        if (binauralEffect == IntPtr.Zero)
            Debug.LogError("[DirectPhononSpatializer] Awake() - binauralEffect is NULL after init! No audio will be spatialized.");
    }

    void InitPhonon()
    {
        Debug.Log("[DirectPhononSpatializer] InitPhonon() - Starting Phonon context creation...");

        var contextSettings = new IPLContextSettings
        {
            version  = 0x00040801,
            simdLevel = 0,
            flags    = 0
        };
        Debug.Log($"[DirectPhononSpatializer] InitPhonon() - IPLContextSettings: " +
                  $"version=0x{contextSettings.version:X8}, simdLevel={contextSettings.simdLevel}, flags={contextSettings.flags}, " +
                  $"logCallback={contextSettings.logCallback}, allocateCallback={contextSettings.allocateCallback}, freeCallback={contextSettings.freeCallback}");

        int ctxResult = iplContextCreate(ref contextSettings, out phononContext);
        Debug.Log($"[DirectPhononSpatializer] InitPhonon() - iplContextCreate result={ctxResult} (0=IPL_STATUS_SUCCESS), phononContext={phononContext}");
        if (ctxResult != 0)
            Debug.LogError($"[DirectPhononSpatializer] InitPhonon() - iplContextCreate FAILED with error code {ctxResult}! Check phonon DLL version matches version field 0x04000000.");

        var audioSettings = new IPLAudioSettings
        {
            samplingRate = sampleRate,
            frameSize    = bufferSize
        };
        Debug.Log($"[DirectPhononSpatializer] InitPhonon() - IPLAudioSettings: samplingRate={audioSettings.samplingRate}, frameSize={audioSettings.frameSize}");

        var hrtfSettings = new IPLHRTFSettings
        {
            type         = IPLHRTFType.Default,
            volume       = 1.0f,
            normType     = 0,
            sofaFileName = IntPtr.Zero,
            sofaData     = IntPtr.Zero,
            sofaDataSize = 0
        };
        Debug.Log($"[DirectPhononSpatializer] InitPhonon() - IPLHRTFSettings: type={hrtfSettings.type}, volume={hrtfSettings.volume}, normType={hrtfSettings.normType}");

        int hrtfResult = iplHRTFCreate(phononContext, ref audioSettings, ref hrtfSettings, out hrtf);
        Debug.Log($"[DirectPhononSpatializer] InitPhonon() - iplHRTFCreate result={hrtfResult} (0=IPL_STATUS_SUCCESS), hrtf={hrtf}");
        if (hrtfResult != 0)
            Debug.LogError($"[DirectPhononSpatializer] InitPhonon() - iplHRTFCreate FAILED with error code {hrtfResult}!");
        if (hrtf == IntPtr.Zero)
            Debug.LogError("[DirectPhononSpatializer] InitPhonon() - hrtf pointer is NULL even though iplHRTFCreate returned. Cannot create binaural effect.");

        var effectSettings = new IPLBinauralEffectSettings
        {
            hrtf = hrtf
        };
        Debug.Log($"[DirectPhononSpatializer] InitPhonon() - IPLBinauralEffectSettings: hrtf={effectSettings.hrtf}");

        int effectResult = iplBinauralEffectCreate(phononContext, ref audioSettings, ref effectSettings, out binauralEffect);
        Debug.Log($"[DirectPhononSpatializer] InitPhonon() - iplBinauralEffectCreate result={effectResult} (0=IPL_STATUS_SUCCESS), binauralEffect={binauralEffect}");
        if (effectResult != 0)
            Debug.LogError($"[DirectPhononSpatializer] InitPhonon() - iplBinauralEffectCreate FAILED with error code {effectResult}!");

        Debug.Log("[DirectPhononSpatializer] InitPhonon() - Done.");
    }

    void Update()
    {
        var camera = Camera.main;
        var audioListenerObj = FindObjectOfType<AudioListener>();
        var listener = camera?.transform ?? audioListenerObj?.transform;

        if (listener == null)
        {
            Debug.LogWarning("[DirectPhononSpatializer] Update() - No listener found! " +
                             "Camera.main is null and no AudioListener found in scene. Direction cannot be computed.");
            return;
        }

        // Log listener source every ~60 frames to avoid spam
        if (Time.frameCount % 60 == 0)
        {
            string listenerSource = camera != null ? $"Camera.main ('{camera.gameObject.name}')" 
                                                   : $"AudioListener ('{audioListenerObj.gameObject.name}')";
            Debug.Log($"[DirectPhononSpatializer] Update() [frame {Time.frameCount}] - Using listener from: {listenerSource}");
            Debug.Log($"[DirectPhononSpatializer] Update() [frame {Time.frameCount}] - Listener pos={listener.position}, rot={listener.eulerAngles}");
            Debug.Log($"[DirectPhononSpatializer] Update() [frame {Time.frameCount}] - Source pos={transform.position}");
        }

        Vector3 sourcePos   = transform.position;
        Vector3 listenerPos = listener.position;
        Vector3 dirToSource = listener.InverseTransformPoint(sourcePos).normalized;
        
        float distance   = Vector3.Distance(listenerPos, sourcePos);
        float maxDistance = audioSource != null && audioSource.maxDistance > 0 ? audioSource.maxDistance : 50f;
        float atten      = Mathf.Clamp01(1.0f - (distance / maxDistance));

        if (Time.frameCount % 60 == 0)
        {
            Debug.Log($"[DirectPhononSpatializer] Update() [frame {Time.frameCount}] - " +
                      $"distance={distance:F2}m, maxDistance={maxDistance:F2}m, attenuation={atten:F4}");
            Debug.Log($"[DirectPhononSpatializer] Update() [frame {Time.frameCount}] - " +
                      $"dirToSource (listener-space, Unity)=({dirToSource.x:F3}, {dirToSource.y:F3}, {dirToSource.z:F3})");
            Debug.Log($"[DirectPhononSpatializer] Update() [frame {Time.frameCount}] - " +
                      $"dirToSource (Phonon, flipped Z)=({dirToSource.x:F3}, {dirToSource.y:F3}, {-dirToSource.z:F3})");

            if (atten <= 0f)
                Debug.LogWarning($"[DirectPhononSpatializer] Update() [frame {Time.frameCount}] - " +
                                 $"Attenuation is ZERO or less ({atten})! Source is beyond maxDistance ({maxDistance}m). " +
                                 $"Audio will be silent. distance={distance:F2}m");
            if (audioSource != null && !audioSource.isPlaying)
                Debug.LogWarning($"[DirectPhononSpatializer] Update() [frame {Time.frameCount}] - " +
                                 $"AudioSource is NOT playing! Make sure Play() is called or playOnAwake=true.");
            if (audioSource != null && audioSource.mute)
                Debug.LogWarning($"[DirectPhononSpatializer] Update() [frame {Time.frameCount}] - AudioSource is MUTED!");
            if (audioSource != null && audioSource.volume <= 0f)
                Debug.LogWarning($"[DirectPhononSpatializer] Update() [frame {Time.frameCount}] - AudioSource.volume is {audioSource.volume}! No output possible.");
            if (audioSource != null && audioSource.clip == null)
                Debug.LogWarning($"[DirectPhononSpatializer] Update() [frame {Time.frameCount}] - AudioSource.clip is NULL! Nothing to play.");
        }

        // Write to thread-shared variables
        lock (audioLock)
        {
            currentDistanceAttenuation = atten;
            currentDirection = new IPLVector3
            {
                x =  dirToSource.x,
                y =  dirToSource.y,
                z = -dirToSource.z // Unity Z to Phonon Z (right-handed)
            };
        }
    }

    // Unsafe is required to pass fast pointers to Steam Audio without GC Allocations
    unsafe void OnAudioFilterRead(float[] data, int channels)
    {
        _audioCallbackCount++;
        bool shouldLog = (_audioCallbackCount % AUDIO_LOG_INTERVAL == 1); // log on 1, 101, 201...

        if (shouldLog)
            Debug.Log($"[DirectPhononSpatializer] OnAudioFilterRead() callback #{_audioCallbackCount} - " +
                      $"data.Length={data.Length}, channels={channels}");

        if (channels != 2)
        {
            Debug.LogError($"[DirectPhononSpatializer] OnAudioFilterRead() - Expected 2 channels (stereo) but got {channels}! " +
                           $"OnAudioFilterRead will do nothing. Check AudioSettings.speakerMode (currently {AudioSettings.speakerMode}).");
            return;
        }

        lock (audioLock)
        {
            if (shouldLog)
                Debug.Log($"[DirectPhononSpatializer] OnAudioFilterRead() #{_audioCallbackCount} - Acquired audioLock.");

            if (binauralEffect == IntPtr.Zero)
            {
                Debug.LogError($"[DirectPhononSpatializer] OnAudioFilterRead() #{_audioCallbackCount} - " +
                               $"binauralEffect is NULL! InitPhonon likely failed. Passing audio through unmodified.");
                return;
            }

            int numSamples = data.Length / channels;

            if (shouldLog)
                Debug.Log($"[DirectPhononSpatializer] OnAudioFilterRead() #{_audioCallbackCount} - " +
                          $"numSamples={numSamples}, monoIn.Length={monoIn.Length}, " +
                          $"currentDistanceAttenuation={currentDistanceAttenuation:F4}, " +
                          $"currentDirection=({currentDirection.x:F3}, {currentDirection.y:F3}, {currentDirection.z:F3})");

            // Expand buffers if Unity unexpectedly gives us a larger DSP chunk
            if (numSamples > monoIn.Length)
            {
                Debug.LogWarning($"[DirectPhononSpatializer] OnAudioFilterRead() #{_audioCallbackCount} - " +
                                 $"numSamples ({numSamples}) > monoIn.Length ({monoIn.Length})! " +
                                 $"Re-allocating buffers. This causes a GC allocation on the audio thread.");
                monoIn  = new float[numSamples];
                leftOut = new float[numSamples];
                rightOut= new float[numSamples];
            }

            // Inspect raw input signal to detect silence BEFORE processing
            if (shouldLog)
            {
                float maxInputSample = 0f;
                float sumInputSample = 0f;
                for (int i = 0; i < data.Length; i++)
                {
                    float abs = Mathf.Abs(data[i]);
                    if (abs > maxInputSample) maxInputSample = abs;
                    sumInputSample += abs;
                }
                float avgInputSample = data.Length > 0 ? sumInputSample / data.Length : 0f;
                Debug.Log($"[DirectPhononSpatializer] OnAudioFilterRead() #{_audioCallbackCount} - " +
                          $"INPUT signal stats: maxAbs={maxInputSample:F6}, avgAbs={avgInputSample:F6}" +
                          (maxInputSample < 1e-7f ? " <<< INPUT IS SILENT/NEAR-ZERO - AudioSource may not be playing or clip is silent!" : ""));
            }

            // Mix down to mono & apply our custom distance attenuation
            for (int i = 0; i < numSamples; i++)
            {
                float mixedMono = (data[i * channels] + data[i * channels + 1]) * 0.5f;
                monoIn[i] = mixedMono * currentDistanceAttenuation;
            }

            // Inspect mono mix to detect issues introduced by downmix/attenuation
            if (shouldLog)
            {
                float maxMonoSample = 0f;
                for (int i = 0; i < numSamples; i++)
                {
                    float abs = Mathf.Abs(monoIn[i]);
                    if (abs > maxMonoSample) maxMonoSample = abs;
                }
                Debug.Log($"[DirectPhononSpatializer] OnAudioFilterRead() #{_audioCallbackCount} - " +
                          $"After mono downmix+attenuation: maxAbs monoIn={maxMonoSample:F6}" +
                          (maxMonoSample < 1e-7f ? " <<< MONO IS SILENT after downmix! Check attenuation & input." : ""));
            }

            // Pin arrays in memory to safely get their pointers
            fixed (float* pMono = monoIn, pLeft = leftOut, pRight = rightOut)
            {
                IntPtr* pInPtrs = stackalloc IntPtr[1];
                pInPtrs[0] = (IntPtr)pMono;

                IntPtr* pOutPtrs = stackalloc IntPtr[2];
                pOutPtrs[0] = (IntPtr)pLeft;
                pOutPtrs[1] = (IntPtr)pRight;

                var inBuffer = new IPLAudioBuffer
                {
                    numChannels = 1,
                    numSamples  = numSamples,
                    data        = (IntPtr)pInPtrs
                };

                var outBuffer = new IPLAudioBuffer
                {
                    numChannels = 2,
                    numSamples  = numSamples,
                    data        = (IntPtr)pOutPtrs
                };

                var effectParams = new IPLBinauralEffectParams
                {
                    direction     = currentDirection,
                    interpolation = IPLHRTFInterpolation.Bilinear,
                    spatialBlend  = 1.0f,
                    hrtf          = hrtf,
                    peakDelays    = IntPtr.Zero
                };

                if (shouldLog)
                    Debug.Log($"[DirectPhononSpatializer] OnAudioFilterRead() #{_audioCallbackCount} - " +
                              $"Calling iplBinauralEffectApply: " +
                              $"effect={binauralEffect}, hrtf={hrtf}, " +
                              $"inBuffer(ch={inBuffer.numChannels}, n={inBuffer.numSamples}, data={inBuffer.data}), " +
                              $"outBuffer(ch={outBuffer.numChannels}, n={outBuffer.numSamples}, data={outBuffer.data}), " +
                              $"params(dir=({effectParams.direction.x:F3},{effectParams.direction.y:F3},{effectParams.direction.z:F3}), " +
                              $"interp={effectParams.interpolation}, spatialBlend={effectParams.spatialBlend}, hrtf={effectParams.hrtf}, peakDelays={effectParams.peakDelays})");

                int applyResult = iplBinauralEffectApply(binauralEffect, ref effectParams, ref inBuffer, ref outBuffer);

                if (shouldLog)
                    Debug.Log($"[DirectPhononSpatializer] OnAudioFilterRead() #{_audioCallbackCount} - " +
                              $"iplBinauralEffectApply returned={applyResult} " +
                              $"(expected 0=IPL_AUDIOEFFECT_TAIL_REMAINS or 1=IPL_AUDIOEFFECT_TAIL_COMPLETE)" +
                              (applyResult < 0 ? $" <<< NEGATIVE return code may indicate an ERROR! code={applyResult}" : ""));
            }

            // Inspect output to check Phonon actually produced non-silent audio
            if (shouldLog)
            {
                float maxLeft = 0f, maxRight = 0f;
                for (int i = 0; i < numSamples; i++)
                {
                    float la = Mathf.Abs(leftOut[i]);
                    float ra = Mathf.Abs(rightOut[i]);
                    if (la > maxLeft)  maxLeft  = la;
                    if (ra > maxRight) maxRight = ra;
                }
                Debug.Log($"[DirectPhononSpatializer] OnAudioFilterRead() #{_audioCallbackCount} - " +
                          $"OUTPUT after iplBinauralEffectApply: maxAbs L={maxLeft:F6}, R={maxRight:F6}" +
                          (maxLeft < 1e-7f && maxRight < 1e-7f ? " <<< OUTPUT IS SILENT! Phonon produced no audio." : ""));
            }

            // Write processed spatialized data back to Unity's buffer
            for (int i = 0; i < numSamples; i++)
            {
                data[i * channels]     = leftOut[i];
                data[i * channels + 1] = rightOut[i];
            }

            // Final sanity check on what we wrote into Unity's buffer
            if (shouldLog)
            {
                float maxWritten = 0f;
                for (int i = 0; i < data.Length; i++)
                {
                    float abs = Mathf.Abs(data[i]);
                    if (abs > maxWritten) maxWritten = abs;
                }
                Debug.Log($"[DirectPhononSpatializer] OnAudioFilterRead() #{_audioCallbackCount} - " +
                          $"FINAL Unity buffer maxAbs={maxWritten:F6}" +
                          (maxWritten < 1e-7f ? " <<< UNITY BUFFER IS SILENT after writing spatialized audio!" : " - Audio looks non-silent, good."));
            }

            isRead = true;
        }
    }

    void OnDestroy()
    {
        Debug.Log($"[DirectPhononSpatializer] OnDestroy() called on '{gameObject.name}'. " +
                  $"Total OnAudioFilterRead callbacks received: {_audioCallbackCount}. isRead={isRead}");
        Debug.Log($"[DirectPhononSpatializer] OnDestroy() - Releasing: binauralEffect={binauralEffect}, hrtf={hrtf}, phononContext={phononContext}");

        // Lock ensures we don't destroy resources mid-way through audio processing
        lock (audioLock)
        {
            if (binauralEffect != IntPtr.Zero)
            {
                iplBinauralEffectRelease(ref binauralEffect);
                Debug.Log("[DirectPhononSpatializer] OnDestroy() - iplBinauralEffectRelease called.");
            }
            else
            {
                Debug.LogWarning("[DirectPhononSpatializer] OnDestroy() - binauralEffect was already NULL, skipping release.");
            }

            if (hrtf != IntPtr.Zero)
            {
                iplHRTFRelease(ref hrtf);
                Debug.Log("[DirectPhononSpatializer] OnDestroy() - iplHRTFRelease called.");
            }
            else
            {
                Debug.LogWarning("[DirectPhononSpatializer] OnDestroy() - hrtf was already NULL, skipping release.");
            }

            if (phononContext != IntPtr.Zero)
            {
                iplContextRelease(ref phononContext);
                Debug.Log("[DirectPhononSpatializer] OnDestroy() - iplContextRelease called.");
            }
            else
            {
                Debug.LogWarning("[DirectPhononSpatializer] OnDestroy() - phononContext was already NULL, skipping release.");
            }
        }

        Debug.Log("[DirectPhononSpatializer] OnDestroy() - Cleanup complete.");
    }
}

// Fixed struct definitions mapping exactly to the C header
[StructLayout(LayoutKind.Sequential)]
struct IPLContextSettings 
{ 
    public uint version; 
    public IntPtr logCallback; 
    public IntPtr allocateCallback; 
    public IntPtr freeCallback; 
    public int simdLevel; 
    public int flags;
}

[StructLayout(LayoutKind.Sequential)]
struct IPLAudioSettings { public int samplingRate; public int frameSize; }

[StructLayout(LayoutKind.Sequential)]
struct IPLVector3 { public float x, y, z; }

[StructLayout(LayoutKind.Sequential)]
struct IPLHRTFSettings 
{ 
    public IPLHRTFType type; 
    public IntPtr sofaFileName; 
    public IntPtr sofaData; 
    public int sofaDataSize; 
    public float volume;
    public int normType; 
}

[StructLayout(LayoutKind.Sequential)]
struct IPLBinauralEffectSettings { public IntPtr hrtf; }

[StructLayout(LayoutKind.Sequential)]
struct IPLBinauralEffectParams 
{ 
    public IPLVector3 direction; 
    public IPLHRTFInterpolation interpolation; 
    public float spatialBlend; 
    public IntPtr hrtf; 
    public IntPtr peakDelays; 
}

[StructLayout(LayoutKind.Sequential)]
struct IPLAudioBuffer 
{ 
    public int numChannels; 
    public int numSamples; 
    public IntPtr data;
}

enum IPLHRTFType { Default = 0, SOFA = 1 }
enum IPLHRTFInterpolation { NearestNeighbor = 0, Bilinear = 1 }

using System;
using System.IO;
using System.Runtime.InteropServices;
using SteamAudio;
using UnityEngine;

namespace PhononSpatializerProxy.BepInEx;

public static class SteamAudioInitializer
{
    public static bool _initialized { get; private set; }

    [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadLibraryW(string lpFileName);

    public static void Initialize()
    {

        if (_initialized)
        {
            Debug.Log("[SteamAudio] Already initialized, skipping.");
            return;
        }

        try
        {
            PreloadNativeDlls();
            EnsureSettings();
            EnsureManager();

            _initialized = true;
            Debug.Log("[SteamAudio] Initialization complete.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SteamAudio] Initialization failed: {ex}");
        }
    }

    private static void PreloadNativeDlls()
    {

        Debug.Log($"[SteamAudio] Pre-loading native DLLs from: {PhononSpatializerProxyPlugin.pathToBinaries}");

        string[] libs = { "phonon.dll", "audioplugin_phonon.dll" };

        foreach (string libName in libs)
        {
            string fullPath = Path.Combine(PhononSpatializerProxyPlugin.pathToBinaries, libName);
            Debug.Log(fullPath);
            if (!File.Exists(fullPath))
            {
                Debug.LogWarning($"[SteamAudio] Native library not found: '{fullPath}' – skipping.");
                continue;
            }

            IntPtr handle = LoadLibraryW(fullPath);
            if (handle == IntPtr.Zero)
            {
                int err = Marshal.GetLastWin32Error();
                Debug.LogError($"[SteamAudio] LoadLibraryW failed for '{libName}' " + $"(Win32 error {err}" + "Missing a dependency DLL?");
            }
            else
            {
                Debug.Log($"[SteamAudio] Pre-loaded: {fullPath}");
            }
        }
    }

    private static void EnsureSettings()
    {
        var settings = SteamAudioSettings.Singleton;

        settings.audioEngine = AudioEngineType.Unity;
        settings.sceneType = SceneType.Default;
        settings.reflectionEffectType = ReflectionEffectType.Parametric;
        settings.hrtfVolumeGainDB = 0f;
        settings.hrtfNormalizationType = HRTFNormType.None;

        settings.SOFAFiles ??= Array.Empty<SOFAFile>();

        if (settings.defaultMaterial == null)
        {
            settings.defaultMaterial = ScriptableObject.CreateInstance<SteamAudioMaterial>();
        }

        settings.realTimeRays = 1024;
        settings.realTimeBounces = 2;
        settings.realTimeDuration = 1.0f;
        settings.realTimeAmbisonicOrder = 1;
        settings.realTimeMaxSources = 32;
        settings.realTimeCPUCoresPercentage = 25;
        settings.maxOcclusionSamples = 16;

        settings.layerMask = LayerMask.GetMask("Default", "HighPolyCollider");
    }

    private static void EnsureManager()
    {
        if (SteamAudioManager.Singleton == null)
        {
            SteamAudioManager.Initialize(ManagerInitReason.Playing);
            Debug.Log("[SteamAudio] SteamAudioManager.Initialize() calleDebug.");
        }
        else
        {
            Debug.Log("[SteamAudio] SteamAudioManager already running.");
        }
    }
}

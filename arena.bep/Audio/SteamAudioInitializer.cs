using Comfort.Common;
using SteamAudio;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;
using ifp.arena.bep;

namespace ifp.arena.shared
{
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


        // ─────────────────────────────────────────────────────────────────────────
        //  0. Native DLL pre-load
        // ─────────────────────────────────────────────────────────────────────────

        private static void PreloadNativeDlls()
        {

            Debug.Log($"[SteamAudio] Pre-loading native DLLs from: {Plugin.pathToDeps}");

            // phonon.dll is the core Phonon/Steam Audio native library that SteamAudioUnity.dll
            // P/Invokes into.  It must be loaded BEFORE audioplugin_phonon.dll.
            string[] libs = { "phonon.dll", "audioplugin_phonon.dll" };

            foreach (string libName in libs)
            {
                string fullPath = Path.Combine(Plugin.pathToDeps, libName);
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
            if (SteamAudioSettings.Singleton != null)
            {
                // The singleton was auto-created by [RuntimeInitializeOnLoadMethod] inside
                // SteamAudioUnity.dll when BepInEx loaded it.  That code path may leave
                // reference-type fields null (e.g. SOFAFiles), which causes NREs later in
                // OnApplicationStart.  Patch them up here.
                var existing = SteamAudioSettings.Singleton;

                if (existing.SOFAFiles == null)
                {
                    existing.SOFAFiles = Array.Empty<SOFAFile>();
                    Debug.Log("[SteamAudio] Patched null SOFAFiles on existing SteamAudioSettings.");
                }

                if (existing.defaultMaterial == null)
                {
                    existing.defaultMaterial = ScriptableObject.CreateInstance<SteamAudioMaterial>();
                    Debug.Log("[SteamAudio] Patched null defaultMaterial on existing SteamAudioSettings.");
                }

                Debug.Log("[SteamAudio] SteamAudioSettings already exists.");
                return;
            }

            // Create a minimal settings object with sensible Phase-1 defaults.
            var settings = ScriptableObject.CreateInstance<SteamAudioSettings>();

            // Audio engine
            settings.audioEngine = AudioEngineType.Unity;

            // Scene type: Default (PhononLib CPU raytracer).  Embree/RadeonRays require extra DLLs.
            settings.sceneType = SceneType.Default;

            // Reflection effect: Convolution (CPU).  TrueAudioNext needs OpenCL.
            settings.reflectionEffectType = ReflectionEffectType.Convolution;

            // HRTF
            settings.hrtfVolumeGainDB = 0f;
            settings.hrtfNormalizationType = HRTFNormType.None;
            settings.SOFAFiles = Array.Empty<SOFAFile>();   // use default built-in HRTF

            // Simulation quality (real-time, low cost for Phase 1 – reflections disabled anyway)
            settings.realTimeRays = 1024;
            settings.realTimeBounces = 2;
            settings.realTimeDuration = 1.0f;
            settings.realTimeAmbisonicOrder = 1;
            settings.realTimeMaxSources = 32;
            settings.realTimeCPUCoresPercentage = 25;

            // Occlusion
            settings.maxOcclusionSamples = 16;

            // Default material (absorptive concrete; used if geometry is added in Phase 2)
            var mat = ScriptableObject.CreateInstance<SteamAudioMaterial>();
            settings.defaultMaterial = mat;

            // Physics layer mask: use "Default" layer for Steam Audio raycasts
            settings.layerMask = LayerMask.GetMask("Default");

            // Inject into the private static singleton field via reflection
            var field = typeof(SteamAudioSettings).GetField("sSingleton", BindingFlags.NonPublic | BindingFlags.Static);

            if (field != null)
            {
                field.SetValue(null, settings);
                Debug.Log("[SteamAudio] SteamAudioSettings created at runtime.");
            }
            else
            {
                Debug.LogError("[SteamAudio] Could not find SteamAudioSettings.sSingleton field – " +
                              "check SteamAudio.dll version.");
            }
        }

        private static void EnsureManager()
        {
            if (SteamAudioManager.Singleton == null)
            {
                // This creates the "Steam Audio Manager" DontDestroyOnLoad GameObject and starts the
                // simulation threaDebug.  Equivalent to [RuntimeInitializeOnLoadMethod] AutoInitialize().
                SteamAudioManager.Initialize(ManagerInitReason.Playing);
                Debug.Log("[SteamAudio] SteamAudioManager.Initialize() calleDebug.");
            }
            else
            {
                Debug.Log("[SteamAudio] SteamAudioManager already running.");
            }

            // Attach the scene tracker to the SteamAudioManager GameObject so it can bridge
            // MapAssetBundleHandler load/unload events → Phase 1 / Phase 2 transitions.
            if (SteamAudioManager.Singleton != null)
            {
                SteamAudioSceneTracker.Register(SteamAudioManager.Singleton.gameObject);
            }
            else
            {
                Debug.LogError("[SteamAudio] SteamAudioManager.Singleton is still null after Initialize() – " +
                              "SteamAudioSceneTracker will not be registereDebug. Occlusion/reflections disableDebug.");
            }
        }

        public static void AttachListenerIfNeeded()
        {
            if (SteamAudioManager.Singleton == null) return;

            var betterAudio = Singleton<BetterAudio>.Instance;
            if (betterAudio == null) return;

            Transform listenerTransform = betterAudio.ListenerTransform != null ? betterAudio.ListenerTransform : betterAudio.AudioListener?.transform;

            if (listenerTransform == null)
            {
                Debug.LogWarning("[SteamAudio] ListenerTransform is null – SteamAudioListener not attached yet.");
                return;
            }

            listenerTransform.gameObject.GetOrAddComponent<SteamAudioListener>();
            SteamAudioManager.NotifyAudioListenerChangedTo(listenerTransform);
            Debug.Log($"[SteamAudio] SteamAudioListener attached to '{listenerTransform.gameObject.name}'.");
        }
    }
}

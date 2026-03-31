using BepInEx.Logging;
using Comfort.Common;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;

#if DEBUG // STEAMAUDIO
using SteamAudio;
#endif

namespace ifp.arena.bep.Audio
{
    /// <summary>
    /// Bootstraps Steam Audio into a running Tarkov session.
    ///
    /// Call <see cref="Initialize"/> once from <c>Plugin.Start()</c> – before any BetterAudio
    /// source pools are created.  Subsequent calls are safe (idempotent).
    ///
    /// Responsibilities
    /// ─────────────────
    ///  0. Pre-load native phonon.dll via kernel32 LoadLibraryW so Mono's P/Invoke
    ///     resolver can find it even when it lives in a BepInEx sub-folder.
    ///
    ///  1. Create a runtime <c>SteamAudioSettings</c> ScriptableObject so
    ///     <c>SteamAudioManager</c> doesn't try to load one from Resources (which doesn't
    ///     exist in a mod context).
    ///
    ///  2. Switch Unity's spatializer plugin from Meta XR to Steam Audio at runtime via
    ///     <c>AudioSettings.Reset()</c>.
    ///
    ///  3. Call <c>SteamAudioManager.Initialize()</c> if it hasn't already been called
    ///     (SPT/Fika uses Mono; <c>[RuntimeInitializeOnLoadMethod]</c> may fire for the
    ///     injected assembly but it's safer to call it explicitly).
    ///
    ///  4. Subscribe to <c>BetterAudio.ListenerSpawned</c> so we can attach
    ///     <c>SteamAudioListener</c> to the player's audio listener transform.
    /// </summary>
    public static class SteamAudioInitializer
    {
        private static ManualLogSource _log;
        private static bool _initialized;

        /// <summary>Name Unity expects for the Steam Audio spatializer plugin.</summary>
        private const string SteamAudioSpatializerName = "Steam Audio Spatializer";

        // ── Windows kernel32 – force-load a native DLL by absolute path ───────────
        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibraryW(string lpFileName);

        public static void Initialize(ManualLogSource log)
        {
            _log = log;

            if (_initialized)
            {
                _log.LogInfo("[SteamAudio] Already initialized, skipping.");
                return;
            }

#if DEBUG // STEAMAUDIO
            try
            {
                PreloadNativeDlls();   // ← must run first; EnsureManager P/Invokes phonon
                EnsureSettings();
                SwitchSpatializerPlugin();
                EnsureManager();
                SubscribeToListenerEvents();

                _initialized = true;
                _log.LogInfo("[SteamAudio] Initialization complete.");
            }
            catch (Exception ex)
            {
                _log.LogError($"[SteamAudio] Initialization failed: {ex}");
            }
#else
            _log.LogWarning("[SteamAudio] Steam Audio support is compiled out (DEBUG not defined).");
#endif
        }

#if DEBUG // STEAMAUDIO

        // ─────────────────────────────────────────────────────────────────────────
        //  0. Native DLL pre-load
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Mono's P/Invoke resolver does not automatically search BepInEx plugin sub-folders.
        /// We force Windows to map phonon.dll (and the Unity audio plugin) into the process
        /// using LoadLibraryW before any managed Steam Audio code executes its first P/Invoke.
        ///
        /// The DLLs are located in the same directory as SteamAudioUnity.dll.
        /// </summary>
        private static void PreloadNativeDlls()
        {
            // Resolve the folder that BepInEx loaded SteamAudioUnity.dll from.
            string dir = Path.GetDirectoryName(typeof(SteamAudioManager).Assembly.Location) ?? AppDomain.CurrentDomain.BaseDirectory;

            _log.LogInfo($"[SteamAudio] Pre-loading native DLLs from: {dir}");

            // phonon.dll is the core Phonon/Steam Audio native library that SteamAudioUnity.dll
            // P/Invokes into.  It must be loaded BEFORE audioplugin_phonon.dll.
            string[] libs = { "phonon.dll", "audioplugin_phonon.dll" };

            foreach (string libName in libs)
            {
                string fullPath = Path.Combine(dir, libName);
                D.Log(fullPath);
                if (!File.Exists(fullPath))
                {
                    _log.LogWarning($"[SteamAudio] Native library not found: '{fullPath}' – skipping.");
                    continue;
                }

                IntPtr handle = LoadLibraryW(fullPath);
                if (handle == IntPtr.Zero)
                {
                    int err = Marshal.GetLastWin32Error();
                    _log.LogError($"[SteamAudio] LoadLibraryW failed for '{libName}' " +
                                  $"(Win32 error {err} – see https://learn.microsoft.com/en-us/windows/win32/debug/system-error-codes). " +
                                  "Missing a dependency DLL?");
                }
                else
                {
                    _log.LogInfo($"[SteamAudio] Pre-loaded: {fullPath}");
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        //  1. Runtime SteamAudioSettings creation
        // ─────────────────────────────────────────────────────────────────────────

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
                    _log.LogInfo("[SteamAudio] Patched null SOFAFiles on existing SteamAudioSettings.");
                }

                if (existing.defaultMaterial == null)
                {
                    existing.defaultMaterial = ScriptableObject.CreateInstance<SteamAudioMaterial>();
                    _log.LogInfo("[SteamAudio] Patched null defaultMaterial on existing SteamAudioSettings.");
                }

                _log.LogInfo("[SteamAudio] SteamAudioSettings already exists.");
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
            var field = typeof(SteamAudioSettings)
                .GetField("sSingleton", BindingFlags.NonPublic | BindingFlags.Static);

            if (field != null)
            {
                field.SetValue(null, settings);
                _log.LogInfo("[SteamAudio] SteamAudioSettings created at runtime.");
            }
            else
            {
                _log.LogError("[SteamAudio] Could not find SteamAudioSettings.sSingleton field – " +
                              "check SteamAudio.dll version.");
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        //  2. Switch spatializer plugin
        // ─────────────────────────────────────────────────────────────────────────

        private static void SwitchSpatializerPlugin()
        {
            // GetSpatializerPluginName() is an [InternalCall] extern – safe against the stub.
            string currentPlugin = UnityEngine.AudioSettings.GetSpatializerPluginName();

            if (currentPlugin == SteamAudioSpatializerName)
            {
                _log.LogInfo("[SteamAudio] Spatializer plugin already set to Steam Audio.");
                return;
            }

            AudioConfiguration cfg = UnityEngine.AudioSettings.GetConfiguration();

            // AudioConfiguration.spatializerPlugin exists at runtime in Unity 2022.3 but is
            // absent from the NuGet compile-time stub (UnityEngine.Modules 2022.3.43 only
            // exposes the 5 primitive fields).  Set it via reflection so the code compiles
            // against the stub while still working at runtime.
            // The struct must be boxed before FieldInfo.SetValue can mutate it – a direct
            // SetValue on an unboxed struct would silently modify a copy and discard it.
            var spatializerField = typeof(AudioConfiguration).GetField(
                "spatializerPlugin",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (spatializerField == null)
            {
                _log.LogError("[SteamAudio] AudioConfiguration.spatializerPlugin field not found " +
                              "at runtime – cannot switch spatializer. Wrong Unity version?");
                return;
            }

            object boxed = cfg;
            spatializerField.SetValue(boxed, SteamAudioSpatializerName);
            cfg = (AudioConfiguration)boxed;

            if (UnityEngine.AudioSettings.Reset(cfg))
            {
                _log.LogInfo($"[SteamAudio] Spatializer switched: '{currentPlugin}' → '{SteamAudioSpatializerName}'.");
            }
            else
            {
                _log.LogWarning("[SteamAudio] AudioSettings.Reset() returned false – spatializer may not have changed. " +
                                "Ensure audioplugin_phonon.dll is in EscapeFromTarkov_Data/Plugins/x86_64/.");
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        //  3. SteamAudioManager init
        // ─────────────────────────────────────────────────────────────────────────

        private static void EnsureManager()
        {
            if (SteamAudioManager.Singleton != null)
            {
                _log.LogInfo("[SteamAudio] SteamAudioManager already running.");
                return;
            }

            // This creates the "Steam Audio Manager" DontDestroyOnLoad GameObject and starts the
            // simulation thread.  Equivalent to [RuntimeInitializeOnLoadMethod] AutoInitialize().
            SteamAudioManager.Initialize(ManagerInitReason.Playing);
            _log.LogInfo("[SteamAudio] SteamAudioManager.Initialize() called.");
        }

        // ─────────────────────────────────────────────────────────────────────────
        //  4. Listener hookup
        // ─────────────────────────────────────────────────────────────────────────

        private static void SubscribeToListenerEvents()
        {
            // BetterAudio.ListenerSpawned fires when SetProtagonist() is called for the local
            // player.  At that point ListenerTransform is already valid.
            //
            // We also patch BetterAudio.SetProtagonist() directly (see
            // Patch_BetterAudio_SetProtagonist) as a belt-and-suspenders approach.
            Singleton<BetterAudio>.Instance?.AudioControllerInitialized += OnAudioControllerInitialized;
        }

        private static void OnAudioControllerInitialized()
        {
            AttachListenerIfNeeded();
        }

#endif

        // ─────────────────────────────────────────────────────────────────────────
        //  Public API – always accessible
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Attaches <c>SteamAudioListener</c> to whatever transform currently has the
        /// Unity <c>AudioListener</c>.  Safe to call multiple times – won't add duplicates.
        /// No-op when Steam Audio is compiled out.
        /// </summary>
        public static void AttachListenerIfNeeded()
        {
#if DEBUG // STEAMAUDIO
            if (SteamAudioManager.Singleton == null) return;

            var betterAudio = Singleton<BetterAudio>.Instance;
            if (betterAudio == null) return;

            Transform listenerTransform = betterAudio.ListenerTransform
                                       ?? betterAudio.AudioListener?.transform;

            if (listenerTransform == null)
            {
                _log?.LogWarning("[SteamAudio] ListenerTransform is null – SteamAudioListener not attached yet.");
                return;
            }

            var existing = listenerTransform.GetComponent<SteamAudioListener>();
            if (existing != null) return;   // already attached

            listenerTransform.gameObject.AddComponent<SteamAudioListener>();
            SteamAudioManager.NotifyAudioListenerChangedTo(listenerTransform);
            _log?.LogInfo($"[SteamAudio] SteamAudioListener attached to '{listenerTransform.gameObject.name}'.");
#endif
        }
    }
}

using System.Collections;
using System.IO;
using Comfort.Common;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Networking;
namespace ifp.arena.bep.Core.Audio
{


    public class MusicManager : MonoBehaviour
    {
        public static MusicManager Instance;

        // PATH SETTINGS
        private const string ROOT_PATH = @"";
        private string _currentKitName = "valve_cs2_01";

        // AUDIO COMPONENTS
        private MusicKit _activeKit;
        private AudioSource _sourceA;
        private AudioSource _sourceB;
        private bool _isSourceAPlaying = false; // Toggles back and forth

        // STATE
        private Coroutine _activeFadeJob;
        private float _fadeDuration = 1.5f;

        void Awake()
        {
            Instance = this;

            // Create two audio sources for crossfading
            _sourceA = gameObject.AddComponent<AudioSource>();
            _sourceB = gameObject.AddComponent<AudioSource>();

            _sourceA.playOnAwake = false;
            _sourceB.playOnAwake = false;
            _sourceA.loop = false;
            _sourceB.loop = false;

            LoadKit(_currentKitName);
        }

        public void LoadKit(string folderName)
        {
            string fullPath = Path.Combine(ROOT_PATH, folderName);
            _activeKit = new MusicKit(fullPath);
            Debug.Log($"[CS2 Music] Kit Loaded: {_activeKit.Name}");
        }

        public void PlayEvent(MusicEvent eventType)
        {
            string filePath = _activeKit.GetRandomTrack(eventType);
            if (string.IsNullOrEmpty(filePath)) return;

            // Stop any existing fade to prevent fighting
            if (_activeFadeJob != null) StopCoroutine(_activeFadeJob);

            _activeFadeJob = StartCoroutine(LoadAndCrossfade(filePath));
        }

        private IEnumerator LoadAndCrossfade(string filePath)
        {
            // 1. Get Target Volume from EFT (so we match game settings)
            float targetVolume = 0.1f; // Default safety
            if (Singleton<EFT.UI.GUISounds>.Instance != null)
            {
                var gameAudio = AccessTools.FieldRefAccess<EFT.UI.GUISounds, AudioSource>("audioSource_0")(Singleton<EFT.UI.GUISounds>.Instance);
                if (gameAudio != null)
                {
                    // We assume the game's UI volume is a good reference for music volume
                    // targetVolume = gameAudio.volume;
                }
            }

            // 2. Load File
            string url = "file://" + filePath;
            AudioType aType = filePath.EndsWith(".wav") ? AudioType.WAV : AudioType.MPEG;

            using (UnityWebRequest uwr = UnityWebRequestMultimedia.GetAudioClip(url, aType))
            {
                yield return uwr.SendWebRequest();

                if (uwr.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"[CS2 Music] Error: {uwr.error}");
                    yield break;
                }

                AudioClip clip = DownloadHandlerAudioClip.GetContent(uwr);
                clip.name = Path.GetFileName(filePath);

                // 3. Determine which source is "In" and which is "Out"
                AudioSource sourceIn = _isSourceAPlaying ? _sourceB : _sourceA;
                AudioSource sourceOut = _isSourceAPlaying ? _sourceA : _sourceB;

                // 4. Setup "In" Source
                sourceIn.clip = clip;
                sourceIn.volume = 0f;
                sourceIn.Play();

                // 5. Perform Crossfade
                float timer = 0f;
                float startVolumeOut = sourceOut.volume;

                while (timer < _fadeDuration)
                {
                    timer += Time.deltaTime;
                    float t = timer / _fadeDuration;

                    // Lerp volumes
                    sourceIn.volume = Mathf.Lerp(0f, targetVolume, t);

                    if (sourceOut.isPlaying)
                    {
                        sourceOut.volume = Mathf.Lerp(startVolumeOut, 0f, t);
                    }

                    yield return null;
                }

                // 6. Cleanup
                sourceIn.volume = targetVolume;
                sourceOut.volume = 0f;
                sourceOut.Stop();
                sourceOut.clip = null; // Free memory

                // Toggle state
                _isSourceAPlaying = !_isSourceAPlaying;
            }
        }
    }

}
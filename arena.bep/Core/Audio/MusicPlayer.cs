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
        private string _currentKitName = "selectiveresponse_01";

        // AUDIO COMPONENTS
        private MusicKit _activeKit;
        private AudioSource _sourceA;
        private AudioSource _sourceB;
        private bool _isSourceAPlaying = false; // Toggles back and forth

        // SETTINGS
        private float _targetVolume = 0.1f;
        private float _crossfadeDuration = 1.5f;
        private float _outroFadeDuration = 1.0f;

        // STATE
        private Coroutine _activeFadeJob;
        private int _playToken = 0; // increments every PlayEvent to invalidate pending outro fades

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

            // Ensure 2D audio (these objects are parented to the player body)
            _sourceA.spatialBlend = 0f;
            _sourceB.spatialBlend = 0f;

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

            _playToken++; // invalidate any pending outro fades

            // Stop any existing fade to prevent fighting
            if (_activeFadeJob != null) StopCoroutine(_activeFadeJob);

            _activeFadeJob = StartCoroutine(LoadAndCrossfade(filePath, eventType, _playToken));
        }

        private IEnumerator LoadAndCrossfade(string filePath, MusicEvent eventType, int token)
        {
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

                bool shouldLoop = eventType == MusicEvent.MainMenu;
                sourceIn.loop = shouldLoop;

                // 4. Setup "In" Source
                sourceIn.clip = clip;
                sourceIn.volume = 0f;
                sourceIn.Play();

                // 5. Perform Crossfade
                float timer = 0f;
                float startVolumeOut = sourceOut.volume;

                // Equal-power crossfade to avoid perceived loudness dip/peak.
                while (timer < _crossfadeDuration)
                {
                    timer += Time.deltaTime;
                    float t = Mathf.Clamp01(timer / _crossfadeDuration);

                    // Equal-power curve: in=sin, out=cos.
                    float inGain = Mathf.Sin(t * Mathf.PI * 0.5f);
                    float outGain = Mathf.Cos(t * Mathf.PI * 0.5f);

                    sourceIn.volume = _targetVolume * inGain;

                    if (sourceOut.isPlaying)
                        sourceOut.volume = startVolumeOut * outGain;

                    yield return null;
                }

                // 6. Cleanup
                sourceIn.volume = _targetVolume;
                sourceOut.volume = 0f;
                sourceOut.Stop();
                sourceOut.clip = null; // Free memory

                // Toggle state
                _isSourceAPlaying = !_isSourceAPlaying;

                // If this clip isn't looping, fade it out at the end (unless interrupted by another PlayEvent).
                if (!shouldLoop)
                    StartCoroutine(OutroFadeWhenNearEnd(sourceIn, token));
            }
        }

        private IEnumerator OutroFadeWhenNearEnd(AudioSource src, int token)
        {
            if (src == null || src.clip == null)
                yield break;

            // If the clip is shorter than the fade duration, just fade immediately.
            float clipLength = src.clip.length;
            float fadeStart = Mathf.Max(0f, clipLength - _outroFadeDuration);

            // Wait until we're close to the end (using src.time to tolerate minor timing drift).
            while (src != null && src.isPlaying && _playToken == token && src.time < fadeStart)
                yield return null;

            if (src == null || !src.isPlaying || _playToken != token)
                yield break;

            float startVol = src.volume;
            float t = 0f;
            while (src != null && src.isPlaying && _playToken == token && t < _outroFadeDuration)
            {
                t += Time.deltaTime;
                float a = Mathf.Clamp01(t / _outroFadeDuration);
                src.volume = Mathf.Lerp(startVol, 0f, a);
                yield return null;
            }

            if (src != null && _playToken == token)
            {
                src.volume = 0f;
                // Allow natural stop, but in case we finished early due to timing issues:
                if (src.isPlaying && src.time < clipLength)
                    yield return new WaitForSeconds(Mathf.Max(0f, clipLength - src.time));

                if (src != null && _playToken == token)
                {
                    src.Stop();
                    src.clip = null;
                }
            }
        }
    }

}
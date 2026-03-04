using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
namespace ifp.arena.bep.Core.Audio
{
    [RequireComponent(typeof(AudioSource))]
    public class MusicPlayer : MonoBehaviour
    {
        [Header("Settings")]
        public string kitFolderName = "valve_cs2_01"; // Change this in Inspector
        public float fadeDuration = 0.5f;

        private AudioSource _audioSource;
        private MusicKit _currentKit;
        private Coroutine _activeFadeJob;

        void Awake()
        {
            _audioSource = GetComponent<AudioSource>();

            // Construct path to StreamingAssets
            string fullPath = Path.Combine(Application.streamingAssetsPath, "MusicKits", kitFolderName);
            _currentKit = new MusicKit(fullPath);

            Debug.Log($"Loaded Kit: {_currentKit.Name}");
        }

        // Call this from your Game Logic
        public void TriggerEvent(MusicEvent musicEvent)
        {
            string filePath = _currentKit.GetRandomTrackPath(musicEvent);

            if (string.IsNullOrEmpty(filePath))
            {
                Debug.LogWarning($"No track found for event: {musicEvent} in kit {_currentKit.Name}");
                return;
            }

            StartCoroutine(LoadAndPlay(filePath));
        }

        IEnumerator LoadAndPlay(string path)
        {
            // 1. Determine Audio Type
            AudioType audioType = AudioType.UNKNOWN;
            if (path.EndsWith(".mp3")) audioType = AudioType.MPEG;
            else if (path.EndsWith(".wav")) audioType = AudioType.WAV;

            // 2. Load from Disk (Must use file:// protocol for Windows/Mac paths)
            string url = "file://" + path;

            using (UnityWebRequest uwr = UnityWebRequestMultimedia.GetAudioClip(url, audioType))
            {
                yield return uwr.SendWebRequest();

                if (uwr.result == UnityWebRequest.Result.ConnectionError || uwr.result == UnityWebRequest.Result.ProtocolError)
                {
                    Debug.LogError(uwr.error);
                }
                else
                {
                    AudioClip clip = DownloadHandlerAudioClip.GetContent(uwr);
                    clip.name = Path.GetFileName(path);

                    // 3. Play with Crossfade
                    if (_activeFadeJob != null) StopCoroutine(_activeFadeJob);
                    _activeFadeJob = StartCoroutine(CrossfadeTo(clip));
                }
            }
        }

        IEnumerator CrossfadeTo(AudioClip newClip)
        {
            float startVolume = _audioSource.volume;

            // Fade Out old if playing
            if (_audioSource.isPlaying)
            {
                for (float t = 0; t < fadeDuration; t += Time.deltaTime)
                {
                    _audioSource.volume = Mathf.Lerp(startVolume, 0, t / fadeDuration);
                    yield return null;
                }
            }

            _audioSource.Stop();
            _audioSource.clip = newClip;
            _audioSource.Play();

            // Fade In new
            for (float t = 0; t < fadeDuration; t += Time.deltaTime)
            {
                _audioSource.volume = Mathf.Lerp(0, 1f, t / fadeDuration); // Assuming max volume 1
                yield return null;
            }
            _audioSource.volume = 1f;
        }
    }
}
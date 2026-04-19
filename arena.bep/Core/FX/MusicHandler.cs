using Comfort.Common;
using Cysharp.Threading.Tasks;
using ifp.arena.bep.Core.Gamemode;
using ifp.arena.bep.GameTypes;
using ifp.arena.bep.networking;
using ifp.arena.shared;
using System;
using System.Threading;
using UnityEngine;

namespace ifp.arena.bep.Core.FX;

public class MusicHandler : Singleton<MusicHandler>, IDisposable
{
    private MusicObject _musicObject;
    private CancellationTokenSource _cts = new CancellationTokenSource();
    
    // Tracks the last second we played a tick for, so we don't spam the sound every frame.
    private int _lastTickSecond = -1;

    public MusicHandler()
    {
        var go = new GameObject("MusicHandler");
        GameObject.DontDestroyOnLoad(go);
        _musicObject = go.AddComponent<MusicObject>();

        if (H.IsHeadless) return;
        EventBus.OnEnter += OnEnter;
        EventBus.OnExit += OnEnd;
        UnityTicker.OnUpdate += Update;
    }

    public void Update() 
    { 
        if (H.Arena == null || H.Session == null) return;

        if (H.Session.matchState == MatchState.RoundPrepare)
        {
            int currentSecond = Mathf.CeilToInt(H.Arena.StateTimer);

            if (currentSecond <= 5 && currentSecond > 0 && currentSecond != _lastTickSecond)
            {
                _lastTickSecond = currentSecond;

                if (H.Sounds?.CountdownTick != null && H.Sounds.CountdownTick.Length > 0)
                {
                    AudioClip tickClip = H.Sounds.CountdownTick.RandomElement();
                    PlaySFX(tickClip, 0.5f);
                }
            }
        }
        else
        {
            // Reset the tracker if we aren't in RoundPrepare
            _lastTickSecond = -1;
        }
    }

    public void OnEnter(MatchState state)
    {
        // return;
        if (state is MatchState.RoundPrepare)
        {
            PlayMusicEvent(H.MusicKit.RoundPrepare.RandomElement());
        }
        else if (state is MatchState.RoundPlanted)
        {
            PlayMusicEvent(H.MusicKit.BombPlanted45);
        }
        else if (state is MatchState.RoundEnd)
        {
            if (H.Arena.LastRoundActionEnd.HasValue)
            {
                Faction winner = H.Arena.LastRoundActionEnd.Value.winner;
                if (winner == H.MainPlayerScore.Faction)
                {
                    PlayMusicEvent(H.MusicKit.RoundWon);
                }
                else
                {
                    PlayMusicEvent(H.MusicKit.RoundLost);
                }
            }
        }
    }

    public void OnEnd(MatchState state)
    {
        if (state is MatchState.RoundPrepare)
        {
            StopMusic(5f);
        }
    }

    public void PlayMusicEvent(AudioClip clip, float crossfadeDuration = 1f)
    {
        if (clip == null) return;

        CancelAndRefreshCts();
        _musicObject.CrossfadeTo(clip, crossfadeDuration, _cts.Token).Forget();
    }
    
    // Dedicated method for One-Shot sounds so the music isn't interrupted
    public void PlaySFX(AudioClip clip, float volume = 1f)
    {
        if (clip == null || _musicObject == null) return;
        _musicObject.PlaySFX(clip, volume);
    }

    public void StopMusic(float fadeDuration = 1f)
    {
        CancelAndRefreshCts();
        _musicObject.FadeOut(fadeDuration, _cts.Token).Forget();
    }

    private void CancelAndRefreshCts()
    {
        _cts.Cancel();
        _cts.Dispose();
        _cts = new CancellationTokenSource();
    }

    public void Dispose()
    {
        EventBus.OnEnter -= OnEnter;
        EventBus.OnExit -= OnEnd;
        UnityTicker.OnUpdate -= Update;

        _cts.Cancel();
        _cts.Dispose();

        if (_musicObject != null)
            GameObject.Destroy(_musicObject.gameObject);

        Release(this);
    }
}

internal class MusicObject : MonoBehaviour
{
    public float MaxVolume = 0.25f;

    private AudioSource _sourceA;
    private AudioSource _sourceB;
    
    // Dedicated source for Sound Effects so music doesn't cut out
    private AudioSource _sfxSource;

    private bool _aIsActive;

    private void Awake()
    {
        _sourceA = gameObject.AddComponent<AudioSource>();
        _sourceB = gameObject.AddComponent<AudioSource>();
        _sfxSource = gameObject.AddComponent<AudioSource>();

        foreach (var src in new[] { _sourceA, _sourceB })
        {
            src.loop = false;
            src.playOnAwake = false;
            src.volume = 0f;
            src.spatialBlend = 0f; // 2D / mono
        }

        _sfxSource.loop = false;
        _sfxSource.playOnAwake = false;
        _sfxSource.spatialBlend = 0f; 
    }

    public void PlaySFX(AudioClip clip, float volume = 1f)
    {
        if (clip != null)
        {
            _sfxSource.PlayOneShot(clip, volume);
        }
    }

    public async UniTask CrossfadeTo(AudioClip clip, float duration, CancellationToken ct)
    {
        AudioSource incoming = _aIsActive ? _sourceB : _sourceA;
        AudioSource outgoing = _aIsActive ? _sourceA : _sourceB;

        incoming.clip = clip;
        incoming.volume = 0f;
        incoming.Play();

        float startVolumeOut = outgoing.volume;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            ct.ThrowIfCancellationRequested();
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            incoming.volume = t * MaxVolume;
            outgoing.volume = Mathf.Lerp(startVolumeOut, 0f, t);
            await UniTask.Yield(PlayerLoopTiming.Update, ct);
        }

        // Snap to final values and clean up the outgoing source.
        incoming.volume = MaxVolume;
        outgoing.volume = 0f;
        outgoing.Stop();
        outgoing.clip = null;

        _aIsActive = !_aIsActive;
    }

    public async UniTask FadeOut(float duration, CancellationToken ct)
    {
        AudioSource active = _aIsActive ? _sourceA : _sourceB;
        AudioSource inactive = _aIsActive ? _sourceB : _sourceA;

        // Silence the idle source immediately in case a partial crossfade left it audible.
        inactive.volume = 0f;
        inactive.Stop();

        float startVolume = active.volume;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            ct.ThrowIfCancellationRequested();
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            active.volume = Mathf.Lerp(startVolume, 0f, t);
            await UniTask.Yield(PlayerLoopTiming.Update, ct);
        }

        active.volume = 0f;
        active.Stop();
        active.clip = null;
    }
}
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

    public MusicHandler()
    {
        var go = new GameObject("MusicHandler");
        GameObject.DontDestroyOnLoad(go);
        _musicObject = go.AddComponent<MusicObject>();

        EventBus.OnEnter += OnEnter;
        EventBus.OnEnd += OnEnd;
        EventBus.OnUpdate += Update;
    }

    public void Update() { }

    public void OnEnter(MatchState state)
    {
        if (state is MatchState.RoundPrepare)
        {
            PlayMusicEvent(H.Sounds.RoundPrepare.RandomElement());
        }
        else if (state is MatchState.RoundPlanted)
        {
            PlayMusicEvent(H.Sounds.BombPlanted45);
        }
        else if (state is MatchState.RoundEnd)
        {
            if (H.Arena.LastRoundActionEnd.HasValue)
            {
                Faction winner = H.Arena.LastRoundActionEnd.Value.winner;
                if (winner == H.MainPlayerScore.faction)
                {
                    PlayMusicEvent(H.Sounds.RoundWon);
                }
                else
                {
                    PlayMusicEvent(H.Sounds.RoundLost);
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
        EventBus.OnEnd -= OnEnd;
        EventBus.OnUpdate -= Update;

        _cts.Cancel();
        _cts.Dispose();

        if (_musicObject != null)
            GameObject.Destroy(_musicObject.gameObject);

        Release(this);
    }
}


internal class MusicObject : MonoBehaviour
{
    public float MaxVolume = 0.5f;

    private AudioSource _sourceA;
    private AudioSource _sourceB;

    private bool _aIsActive;

    private void Awake()
    {
        _sourceA = gameObject.AddComponent<AudioSource>();
        _sourceB = gameObject.AddComponent<AudioSource>();

        foreach (var src in new[] { _sourceA, _sourceB })
        {
            src.loop = false;
            src.playOnAwake = false;
            src.volume = 0f;
            src.spatialBlend = 0f; // 2D / mono
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

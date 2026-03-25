using Comfort.Common;
using Cysharp.Threading.Tasks;
using ifp.arena.bep.Core.AssetBundleHandling;
using ifp.arena.bep.Core.Gamemode;
using ifp.arena.bep.GameTypes;
using ifp.arena.shared.FX;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace ifp.arena.bep.Core.FX
{
    public class BombHandler : Singleton<BombHandler>, IDisposable
    {
        public AssetBundle audioBundle { get; private set; }
        public LambdaSounds prefabSounds { get; private set; }

        public BetterSource LastBombSource { get; private set; }
        public BetterSource LastBombTickSource { get; private set; }

        private CancellationTokenSource _bombTickCancellationSource;

        private bool _beforeExplodingPlayed = false;

        public BombHandler()
        {
            audioBundle = AssetBundle.LoadFromFile(System.IO.Path.Combine(MapAssetBundleHandler.pathToBundlesDir, "audio"));
            prefabSounds = audioBundle.LoadAsset<LambdaSounds>("Assets/Sounds/SoundData.asset");

            EventBus.OnEnter += OnEnter;
            EventBus.OnEnd += OnEnd;
            EventBus.OnUpdate += Update;
        }

        public void Update()
        {
            if (H.Arena.ActiveRules is not SnDModeRules) return;

            if (_beforeExplodingPlayed) return;
            if (H.Arena.StateTimer <= H.Sounds.BeforeExploding.length)
            {
                PlayBombAudio(H.Arena.BombPlantedPosition, H.Sounds.BeforeExploding);
                _beforeExplodingPlayed = true;
            }
        }

        public void OnEnter(MatchState state)
        {
            if (state is MatchState.RoundPlanted)
            {
                _beforeExplodingPlayed = false;
            }
        }

        public void OnEnd(MatchState state)
        {
            if (state is MatchState.RoundPlanted)
            {
                _beforeExplodingPlayed = false;
            }
        }

        public void PlayBombAudio(Vector3 pos, AudioClip clip)
        {
            LastBombSource = H.AudioHandler.PlayAtPoint(pos, clip);
        }

        public void CancelBombAudio()
        {
            LastBombSource.Stop();
        }

        public void StartBombTick(Vector3 pos)
        {
            StopBombTick();

            _bombTickCancellationSource = new CancellationTokenSource();
            Vector3 slightUpPos = pos;
            slightUpPos.y += 3f;
            PlayEverySecondAsync(slightUpPos, H.Sounds.Tick, _bombTickCancellationSource.Token).Forget();
        }

        public void StopBombTick()
        {
            if (_bombTickCancellationSource != null)
            {
                _bombTickCancellationSource.Cancel();
                _bombTickCancellationSource.Dispose();
                _bombTickCancellationSource = null;
            }
        }

        private async UniTaskVoid PlayEverySecondAsync(Vector3 pos, AudioClip clip, CancellationToken token)
        {
            try
            {
                while (true)
                {
                    await UniTask.Delay(1000, cancellationToken: token);

                    if (token.IsCancellationRequested)
                        break;

                    H.AudioHandler.PlayAtPoint(pos, clip);
                }
            }
            catch (OperationCanceledException)
            {
                // expected, ignore
            }
        }

        public void Reset()
        {
            StopBombTick();
            _beforeExplodingPlayed = false;
        }

        public void Dispose()
        {
            Reset();
        }
    }
}

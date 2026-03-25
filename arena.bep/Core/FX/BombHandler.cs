using Comfort.Common;
using Cysharp.Threading.Tasks;
using EFT;
using EFT.CameraControl;
using EFT.Interactive;
using EFT.InventoryLogic;
using EFT.SynchronizableObjects;
using ifp.arena.bep.Core.AssetBundleHandling;
using ifp.arena.bep.Core.Gamemode;
using ifp.arena.bep.GameTypes;
using ifp.arena.bep.networking;
using ifp.arena.shared.FX;
using System;
using System.Collections.Generic;
using System.Threading;
using Systems.Effects;
using UnityEngine;

namespace ifp.arena.bep.Core.FX
{
    public class BombHandler : Singleton<BombHandler>, IDisposable
    {
        public BetterSource LastBombSource { get; private set; }
        public BetterSource LastBombTickSource { get; private set; }

        public GameObject bombVisuals { get; private set; }
        public Vector3 BombPlantedPosition { get; set; } // yes it's not really supposed to be public set;

        private CancellationTokenSource _bombTickCancellationSource;

        private bool _beforeExplodingPlayed = false;
        private bool _isAlreadyPlanted = false;

        public BombHandler()
        {
            InitBombVisualsAsync().Forget();

            EventBus.OnEnter += OnEnter;
            EventBus.OnEnd += OnEnd;
            EventBus.OnUpdate += Update;
        }

        public void Update()
        {
            if (H.Arena is null) return;
            if (H.Arena.ActiveRules is not SnDModeRules) return;

            if (_beforeExplodingPlayed) return;
            if (H.Arena.StateTimer <= H.Sounds.BeforeExploding.length && H.Session.matchState is MatchState.RoundPlanted)
            {
                H.AudioHandler.PlayAtPoint(BombPlantedPosition, H.Sounds.BeforeExploding);
                _beforeExplodingPlayed = true;
            }
        }

        public void OnEnter(MatchState state)
        {
            if (state is MatchState.RoundPlanted or MatchState.RoundPrepare)
            {
                _beforeExplodingPlayed = false;
            }
        }

        public void OnEnd(MatchState state)
        {
            if (state is MatchState.RoundPlanted)
                _isAlreadyPlanted = false;
        }

        public void PlayBombAudio(BombStatePacket packet)
        {
            Player player = H.GetPlayer(packet.playerId);

            Vector3 pos = Vector3.zero;
            AudioClip clip = null;
            bool shouldPlay = true;

            switch (packet.state)
            {
                case BombState.None:
                    CancelBombAudio();
                    break;
                case BombState.Planting:
                    pos = player.PlayerBody.transform.position;
                    clip = H.Sounds.Planting;
                    break;
                case BombState.Defusing:
                    pos = player.PlayerBody.transform.position;
                    clip = H.Sounds.Defusing;
                    break;
                case BombState.Defused:
                    pos = H.BombHandler.BombPlantedPosition;
                    clip = H.Sounds.Defused;
                    StopBombTick();
                    break;
                case BombState.Planted:
                    if (!_isAlreadyPlanted)
                    {
                        pos = H.BombHandler.BombPlantedPosition;
                        clip = H.Sounds.Planted;
                        StartBombTick(pos);
                        _isAlreadyPlanted = true;
                    }
                    else shouldPlay = false;
                    break;
                case BombState.Exploded:
                    pos = H.BombHandler.BombPlantedPosition;
                    clip = H.Sounds.Planted;
                    StopBombTick();
                    break;
                default:
                    shouldPlay = false;
                    break;
            }

            if (shouldPlay && pos != Vector3.zero && clip != null)
            {
                LastBombSource = H.AudioHandler.PlayAtPoint(pos, clip);
            }
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
            slightUpPos.y += 0.5f;
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

        private async UniTaskVoid InitBombVisualsAsync()
        {
            Item bombItem = IU.CreateItemFromTemplateId(SnDModeRules.bombTemplateId);
            await IU.LoadBundlesForItem(bombItem);
            bombVisuals = Singleton<PoolManagerClass>.Instance.CreateLootPrefab(bombItem, ECameraType.Default);
            bombVisuals?.SetActive(false);

            foreach (var component in bombVisuals.GetComponentsInChildren<Component>(true))
            {
                if (component is Renderer or Transform or LODGroup or MeshFilter) continue;
                Component.Destroy(component);
            }

            bombVisuals.GetOrAddComponent<bombasik>();

            UnityEngine.Object.DontDestroyOnLoad(bombVisuals);
        }

        public void SetBombVisuals(BombStatePacket bombStatePacket)
        {
            if (bombStatePacket.state == BombState.Planted)
            {
                BombPlantedPosition = bombStatePacket.position;
                bombVisuals.transform.position = bombStatePacket.position;
            }

            switch (bombStatePacket.state)
            {
                case BombState.Defusing:
                case BombState.Defused:
                case BombState.Planted:
                    bombVisuals?.SetActive(true);
                    break;
                default:
                    bombVisuals?.SetActive(false);
                    break;
            }

            if (bombStatePacket.state == BombState.Exploded)
            {
                Vector3 explosionCenter = bombVisuals.transform.position;
                float distance = Vector3.Distance(explosionCenter, H.MainPlayer.PlayerBody.transform.position);
                if (distance <= 25f)
                {
                    H.MainPlayer.ActiveHealthController.Kill(EDamageType.Explosion);
                }
                Singleton<Effects>.Instance.Emit("Gas_explosion", explosionCenter, Vector3.up * 0.1f);
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
            Release(this);
        }
    }

    public class bombasik : InteractableObject
    {
        void Awake()
        {
            Mesh sharedMesh = this.GetComponentInChildren<MeshFilter>().sharedMesh;
            var collider = this.GetOrAddComponent<BoxCollider>();
            collider.size = sharedMesh.bounds.size;
            collider.center = sharedMesh.bounds.center;
            gameObject.layer = 22;
        }
    }
}

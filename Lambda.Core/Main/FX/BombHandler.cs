using Comfort.Common;
using Cysharp.Threading.Tasks;
using EFT;
using EFT.CameraControl;
using EFT.Interactive;
using EFT.InventoryLogic;
using EFT.SynchronizableObjects;
using Lambda.Core.Main.AssetBundleHandling;
using Lambda.Core.Main.Gamemode;
using Lambda.Core.GameTypes;
using Lambda.Core.Networking;
using Lambda.Shared.FX;
using System;
using System.Collections.Generic;
using System.Threading;
using Systems.Effects;
using UnityEngine;

namespace Lambda.Core.Main.FX;

public class BombHandler : Singleton<BombHandler>, IDisposable
{
    public BetterSource LastBombSource { get; private set; }
    public BetterSource LastBombTickSource { get; private set; }

    public GameObject BombVisuals { get; private set; }
    public Vector3 BombPlantedPosition { get; set; } // yes it's not really supposed to be public set;

    private CancellationTokenSource _bombTickCancellationSource;

    private bool _beforeExplodingPlayed = false;
    private bool _isAlreadyPlanted = false;

    public BombHandler()
    {
        InitBombVisualsAsync().Forget();

        EventBus.OnEnter += OnEnter;
        EventBus.OnExit += OnEnd;
        UnityTicker.OnUpdate += Update;
    }

    public void Dispose()
    {
        EventBus.OnEnter -= OnEnter;
        EventBus.OnExit -= OnEnd;
        UnityTicker.OnUpdate -= Update;

        if (BombVisuals != null)
        {
            UnityEngine.Object.Destroy(BombVisuals);
        }

        Reset();
        Release(this);
    }

    public void Reset()
    {
        StopBombTick();
        CancelBombAudio();
        _beforeExplodingPlayed = false;
        _isAlreadyPlanted = false;
        BombPlantedPosition = Vector3.zero;

        if (BombVisuals != null)
        {
            BombVisuals.transform.position = Vector3.zero;
            BombVisuals.SetActive(false);
        }
    }

    public void Update()
    {
        if (H.Arena == null) return;
        if (H.Gamemode is not SNDGamemode) return;
        if (H.Session == null) return;
        if (H.Sounds == null) return;

        if (_beforeExplodingPlayed) return;
        if (H.Arena?.StateTimer <= H.Sounds.BeforeExploding.length && H.Session.matchState is MatchState.RoundPlanted)
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
        Vector3 pos = Vector3.zero;
        AudioClip clip = null;
        bool shouldPlay = true;

        switch (packet.state)
        {
            case BombState.None:
                CancelBombAudio();
                break;
            case BombState.Planting:
                pos = packet.Player.PlayerBody.transform.position;
                clip = H.Sounds.Planting;
                break;
            case BombState.Defusing:
                pos = packet.Player.PlayerBody.transform.position;
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
                    pos = packet.position;
                    clip = H.Sounds.Planted;
                    StartBombTick(pos);
                    _isAlreadyPlanted = true;
                }
                else shouldPlay = false;
                break;
            case BombState.Exploded:
                // pos = H.BombHandler.BombPlantedPosition;
                // clip = H.Sounds.Planted;
                shouldPlay = false;
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
        LastBombSource?.Stop();
    }

    public void StartBombTick(Vector3 pos)
    {
        StopBombTick();

        _bombTickCancellationSource = new CancellationTokenSource();
        Vector3 slightUpPos = pos;
        slightUpPos.y += 0.05f;
        PlayEverySecondAsync(slightUpPos, H.Sounds.BombTick, _bombTickCancellationSource.Token).Forget();
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

                H.AudioHandler.PlayAtPoint(pos, clip, 75, default, 0.5f);
            }
        }
        catch (OperationCanceledException)
        {
            // expected, ignore
        }
    }

    private async UniTaskVoid InitBombVisualsAsync()
    {
        Item bombItem = IU.CreateItemFromTemplateId(Hardcode.BOMB_BACKPACK);
        await IU.LoadBundlesForItem(bombItem);
        BombVisuals = H.PoolManagerClass.CreateLootPrefab(bombItem, ECameraType.Default);
        BombVisuals?.SetActive(false);

        foreach (var component in BombVisuals.GetComponentsInChildren<Component>(true))
        {
            if (component is Renderer or Transform or LODGroup or MeshFilter) continue;
            Component.Destroy(component);
        }

        BombVisuals.GetOrAddComponent<Bombasik>();

        UnityEngine.Object.DontDestroyOnLoad(BombVisuals);
    }

    public void SetBombVisuals(BombStatePacket bombStatePacket)
    {
        if (bombStatePacket.state == BombState.Planted)
        {
            BombPlantedPosition = bombStatePacket.position;
            BombVisuals.transform.position = bombStatePacket.position;
        }

        switch (bombStatePacket.state)
        {
            case BombState.Defusing:
            case BombState.Defused:
            case BombState.Planted:
                BombVisuals?.SetActive(true);
                break;
            default:
                BombVisuals?.SetActive(false);
                break;
        }

        if (bombStatePacket.state == BombState.Exploded)
        {
            if (!H.IsHeadless)
            {
                H.Effects.Emit("Gas_explosion", bombStatePacket.position, Vector3.up * 0.1f);
            }
        }
    }

}

public class Bombasik : InteractableObject
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

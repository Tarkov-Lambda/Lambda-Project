using Comfort.Common;
using EFT;
using Fika.Core.Modding.Events;
using Fika.Core.Networking.Snapshotting;
using Lambda.Core.Main.AssetBundleHandling;
using Lambda.Core.Main.Dying;
using Lambda.Core.Main.Economy;
using Lambda.Core.Main.UI;
using Lambda.Core.GameTypes;
using Lambda.Core.Networking;
using Lambda.Shared;
using MemoryPack;
using PacketWarden.TimeSync;
using System;
using UnityEngine;
using static Fika.Core.Modding.FikaEventDispatcher;
using System.Collections.Generic;
using EFT.InventoryLogic;

namespace Lambda.Core.Main.Gamemode;

[MemoryPackable]
public partial struct RoundActionPhaseEnd
{
    public int mvpId;
    public string mvpReason;
    public Faction winner;
    public RoundWinReason roundWinReason;
}

// This is the place where we manage both server/client arena behaviour
public class ArenaController : Singleton<ArenaController>, IDisposable
{
    public SessionManager Session { get; private set; } = null;
    public LambdaGamemode gamemode = null;

    // These realistically should reside inside the gamemode
    // however given that LambdaGamemode is in Lambda.Shared which does not have EFT assembly reference
    // it's kind of difficult atm
    public EconomyManager economyManager = new();
    public IRespawnManager respawnManager = new RespawnManager();
    public IInventoryManager inventoryManager = new BaseInventoryManager();

    public float StateTimer;
    public double ServerPhaseStartSeconds, PhaseDurationSeconds;

    // this block of vars just seems wrong
    public RoundActionPhaseEnd? PendingRoundActionEnd;
    public RoundActionPhaseEnd? LastRoundActionEnd;
    public Player LastObjectivePlayer;
    public BombState LastObjectiveBombState = BombState.None; // Defused/Exploded (or None)

    private AbstractMatchStateController _currentState;

    public event Action OnBeginInitializing;
    public event Action OnInitialized;
    public event Action OnBeginDisposing;
    public event Action OnDisposed;

    public ArenaController()
    {
        H.OnGameStarted += StartSession;
        H.OnGameDispose += EndSession;
        OnFikaEvent += ManageFikaEvents;

        if (H.IsInRaid()) StartSession();
    }

    public void Dispose()
    {
        H.OnGameStarted -= StartSession;
        H.OnGameDispose -= EndSession;
        OnFikaEvent -= ManageFikaEvents;
        EndSession();
    }

    public void ManageFikaEvents(FikaEvent fikaEvent)
    {
        if (H.IsClient) return;

        if (fikaEvent is PeerDisconnectedEvent peerDisconnectedEvent)
        {
            if (H.IsClient) return;

            Player player = peerDisconnectedEvent.Peer.Player;
            if (player != null)
            {
                Singleton<PlayerReadinessPacketWarden>.Instance.SendForPlayer(player, PlayerReadinessState.Disconnected);
                var damageInfo = new DamageInfoStruct
                {
                    Damage = 1f,
                    BodyPartColliderType = EBodyPartColliderType.RibcageUp
                };
                Singleton<PlayerKilledPacketWarden>.Instance.Send(damageInfo, player, player);
            }
        }
        else if (fikaEvent is PeerConnectedEvent peerConnectedEvent && H.IsArenaReady)
        {
            // notify 
            Singleton<AssetBundleLoadPacketWarden>.Instance.SendToLateJoiner(peerConnectedEvent.Peer.Id, RuntimeBundleLoader.Instance.ItemsToLoad);
        }
    }

    public async void StartSession()
    {
        OnBeginInitializing?.Invoke();

        UnityTicker.OnUpdate += Update;

        Session = new SessionManager();

        await Singleton<MapAssetBundleLoader>.Instance.LoadMap("lobby");

        var allHardcodedItemTemplateIDs = Hardcode.GetAllTemplateIDs();
        foreach (var hardcodedItemTemplateID in allHardcodedItemTemplateIDs)
        {
            Item item = IU.CreateItemFromTemplateId(hardcodedItemTemplateID);
            if (item != null) RuntimeBundleLoader.Instance.AddToCache(item);
        }

        if (!H.IsHeadless)
        {
            // SteamAudioInitializer.AttachListenerIfNeeded();

            Teleporter.Teleport(H.MainPlayer, "lobby", Faction.None);

            List<Item> allPresetitems = PresetItemsCache.Instance.GetAllPresetItems();
            RuntimeBundleLoader.Instance.AddToCache(allPresetitems);

            HU.ApplyPainkiller();

            H.BackendConfigSettingsClass.AimPunchMagnitude = 1f;

            Singleton<PlayerReadinessPacketWarden>.Instance.Send(PlayerReadinessState.Connected);

            PU.OpenEyes();

            // LambdaAudioRoomController.Instance.TriggerChange();
        }

        OnInitialized?.Invoke();
    }

    public void EndSession()
    {
        OnBeginDisposing?.Invoke();

        _currentState?.OnExit();
        _currentState = null;

        Session = null;

        UnityTicker.OnUpdate -= Update;

        OnDisposed?.Invoke();
    }

    public void Update()
    {
        if (Session == null || _currentState == null) return;

        // On Server: Start + Duration - Now ~= Duration (since Start is Now)
        // On Client: Start + Duration - Now = Remaining Time accurately synced
        StateTimer = (float)(ServerPhaseStartSeconds + PhaseDurationSeconds - NetworkTime.ServerNowSeconds);

        if (H.IsServer)
        {
            MatchState? nextState = _currentState.OnUpdate();
            if (nextState.HasValue)
            {
                ServerPostMatchState(nextState.Value);
            }
        }
    }

    public async void ServerPostMatchState(MatchState newStateType)
    {
        if (H.IsClient) return;

        RoundActionPhaseEnd? roundEndData = PendingRoundActionEnd;
        PendingRoundActionEnd = null;

        Singleton<MatchStateSyncPacketWarden>.Instance.Send(newStateType, H.Gamemode.StateTimerConfig[newStateType], roundEndData);
    }

    public void TransitionToState(MatchStateSyncPacket packet)
    {
        var previousState = _currentState;

        PhaseDurationSeconds = packet.PhaseDurationSeconds;
        ServerPhaseStartSeconds = packet.Timestamp;

        if (packet.roundActionEnd.HasValue)
        {
            LastRoundActionEnd = packet.roundActionEnd.Value;
            EventBus.OnRoundActionEnd?.Invoke(packet.roundActionEnd.Value);
        }

        Session.matchState = packet.matchState;
        _currentState = gamemode.CreateState(packet.matchState);

        StateTimer = (float)(ServerPhaseStartSeconds + PhaseDurationSeconds - NetworkTime.ServerNowSeconds);

        if (previousState != null)
        {
            // Cancel CancellationToken to signal any async sequence running in the OnEnter to do an early return
            previousState.Dispose(); 
            previousState.OnExit();
            EventBus.OnExit?.Invoke(previousState.StateType);
        }

#if DEBUG
        D.LogArenaController($"Entering {_currentState.GetType()} at {NetworkTime.ServerNowSeconds}");
#endif

        if (_currentState != null)
        {
            _currentState.OnEnter();
            EventBus.OnEnter?.Invoke(_currentState.StateType);
        }
    }

    public void Award(Faction w, RoundWinReason reason)
    {
        if (!H.Session.factionWins.ContainsKey(w))
            H.Session.factionWins[w] = 0;
        H.Session.factionWins[w]++;

        var (mvpId, mvpReason) = MvpCalculator.CalculateRoundMvp(w, reason, H.Arena.LastObjectiveBombState, H.Arena.LastObjectivePlayer);

        H.Arena.PendingRoundActionEnd = new RoundActionPhaseEnd { mvpId = mvpId, mvpReason = mvpReason, winner = w, roundWinReason = reason };
    }
}

public class UnityTicker : MonoBehaviour
{
    public static event Action OnUpdate;
    public static event Action OnLateUpdate;

    private void Update() => OnUpdate?.Invoke();
    private void LateUpdate() => OnLateUpdate?.Invoke();
}
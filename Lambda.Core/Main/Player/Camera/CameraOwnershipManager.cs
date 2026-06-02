using System;
using Comfort.Common;
using Lambda.Core.Main.Gamemode;
using Lambda.Core.Networking;

public enum CameraOwnership
{
    EFT,
    Ragdoll,
    Spectator,
    SpectatorRagdoll,
    Cinematic
}


public class CameraOwnershipManager : Singleton<CameraOwnershipManager>, IDisposable
{
    public CameraOwnership CurrentState { get; private set; } = CameraOwnership.EFT;

    CameraOwnershipManager()
    {
        EventBus.OnEnter += OnMatchStateEnter;
        Singleton<PlayerKilledPacketWarden>.Instance.AfterPacketApplied += OnPlayerKilled;

    }

    public void Dispose()
    {
        EventBus.OnEnter -= OnMatchStateEnter;
        Singleton<PlayerKilledPacketWarden>.Instance.AfterPacketApplied -= OnPlayerKilled;
    }

    void OnMatchStateEnter(MatchState state)
    {

    }

    void OnPlayerKilled(PlayerKilledPacket packet)
    {

    }
}
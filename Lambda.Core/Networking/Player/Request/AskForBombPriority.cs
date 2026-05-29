using Comfort.Common;
using EFT;
using Lambda.Core.Main.Gamemode;
using Lambda.Shared.Models;
using MemoryPack;
using PacketWarden.RateLimiting;
using UnityEngine.UIElements;

namespace Lambda.Core.Networking;

[MemoryPackable]
public partial struct AskForBombPriorityPacket : IPacket, IAuthoredPacket
{
    [MemoryPackAllowSerialize]
    public Player Player { get; set; }

    public bool isAsking;
}

public class AskForBombPriorityPacketWarden : LambdaPacketWarden<AskForBombPriorityPacket>
{
    public Player AssignedPlayer { get; private set; } = null;

    protected override RateLimitConfig ServerRateLimit => RateLimitPresets.LimitPerSecond(1, RateLimitAction.Reject);

    protected override bool ShouldNotifyAboutRejection => true;
    protected override bool ShouldDisplayRejectionInChat => true;

    public AskForBombPriorityPacketWarden()
    {
        EventBus.OnEnter += OnEnter;
    }

    public override void Dispose()
    {
        EventBus.OnEnter -= OnEnter;
        base.Dispose();
    }

    // TODO: I can't figure out the architectural pattern with these NGL
    // should the gamemode framework explicitly handle low priority shit like this
    // or should it just be implicit and tidy? if someone is reading this and are an architect please message me
    public void OnEnter(MatchState matchState)
    {
        if (matchState == MatchState.SideSwap)
        {
            AssignedPlayer = null;
        }
    }

    public void Send(bool isAsking)
    {
        var packet = new AskForBombPriorityPacket
        {
            Player = H.MainPlayer,
            isAsking = isAsking
        };
        DispatchPacket(ref packet);
    }

    protected override bool ValidatePacket(AskForBombPriorityPacket packet, int peerId, out string rejectionReason)
    {
        if (H.Gamemode is not SNDGamemode)
        {
            rejectionReason = "This command is not accessible outside of Search And Destroy.";
            return false;
        }
        else if (packet.Player.GetContext().Faction is not Faction.T)
        {
            rejectionReason = "You have to be a terrorist to use this command.";
            return false;
        }

        return base.ValidatePacket(packet, peerId, out rejectionReason);
    }

    protected override void ProcessApprovedPacket(ref AskForBombPriorityPacket packet, int peerId)
    {
        MutateApprovedPacket(ref packet, peerId);
        ApplyInternal(packet, peerId);
    }

    // Handled in ChatController
    protected override void Apply(AskForBombPriorityPacket packet, int peerId)
    {
        var previousAssignedPlayer = packet.Player;

        if (packet.isAsking)
        {
            AssignedPlayer = packet.Player;
        }
        else if (!packet.isAsking && packet.Player == AssignedPlayer)
        {
            AssignedPlayer = null;
        }

        string announcement;

        if (AssignedPlayer == null)
        {
            announcement = $"{packet.Player.Profile.Nickname} no longer wants the bomb.";
        }
        else if (previousAssignedPlayer != null && previousAssignedPlayer != packet.Player)
        {
            announcement = $"{packet.Player.Profile.Nickname} is now first in line for the bomb.";
        }
        else
        {
            announcement = $"{packet.Player.Profile.Nickname} asked for the bomb.";
        }

        Singleton<ServerMessagePacketWarden>.Instance.SendToFaction(AssignedPlayer.GetContext().Faction, announcement);
    }
}
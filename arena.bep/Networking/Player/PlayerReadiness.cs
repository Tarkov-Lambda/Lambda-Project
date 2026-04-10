using Comfort.Common;
using EFT;
using Fika.Core.Main.Utils;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.GameTypes;
using PacketHandler;
using ifp.arena.shared;
using MemoryPack;

namespace ifp.arena.bep.networking;

[MemoryPackable]
public partial struct PlayerReadinessPacket : INetSerializable, IAuthoredPacket
{
    [MemoryPackAllowSerialize]
    public Player Player { get; set; }

    public PlayerReadinessState readyState;
    public int progress;

    public void Serialize(NetDataWriter writer) => MemoryPackWrapper.Serialize(writer, this);
    public void Deserialize(NetDataReader reader) => this = MemoryPackWrapper.Deserialize<PlayerReadinessPacket>(reader);
}

public class PlayerReadinessPacketHandler : PacketHandler<PlayerReadinessPacket>
{
    public void Send(PlayerReadinessState readyState, int progress = 0)
    {
        if (H.IsHeadless) return;

        var packet = new PlayerReadinessPacket
        {
            readyState = readyState,
            progress = progress
        };

        RequestSend(packet);
    }

    protected override void WhenApproved(PlayerReadinessPacket packet, NetPeer peer)
    {
        PlayerScore playerScore = H.GetPlayerScore(packet.Player);
        if (playerScore == null)
        {
            H.Scoreboard[packet.Player.Id] = new PlayerScore(packet.Player.Id);
            playerScore = H.Scoreboard[packet.Player.Id];
        }

        playerScore.readyState = packet.readyState;

        if (!H.IsClient)
        {
            // In case a player is reporting they are connected mid session (reconnects, new joins)
            if (H.Session?.matchState > MatchState.WarmupEnd && packet.readyState == PlayerReadinessState.Connected)
            {
                if (packet.Player == null) return;

                if (!H.Scoreboard.ContainsKey(packet.Player.Id))
                {
                    H.Scoreboard[packet.Player.Id] = new PlayerScore(packet.Player.Id);
                    H.GetPlayerScore(packet.Player.Id).ChangeFaction(Faction.Spectator);
                }

                Singleton<SessionStartPacketHandler>.Instance.SendToPlayer(packet.Player);
                Singleton<SessionInfoPacketHandler>.Instance.SendToPlayer(packet.Player);
            }
        }

        if (packet.Player.IsYourPlayer && packet.readyState == PlayerReadinessState.Connected)
        {
            Singleton<AdminLoginPacketHandler>.Instance.Send();
        }
    }
}
using Comfort.Common;
using EFT;
using Fika.Core.Main.Utils;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.GameTypes;
using ifp.arena.bep.networking.Base;
using ifp.arena.shared;
using MemoryPack;

namespace ifp.arena.bep.networking;

[MemoryPackable]
public partial struct PlayerReadinessPacket : INetSerializable, AuthoredPacket
{
    [MemoryPackAllowSerialize]
    public Player player { get; set; }

    public PlayerReadinessState readyState;
    public int progress;

    public void Serialize(NetDataWriter writer) => MemoryPackHelper.Serialize(writer, this);
    public void Deserialize(NetDataReader reader) => this = MemoryPackHelper.Deserialize<PlayerReadinessPacket>(reader);
}

public class PlayerReadinessPacketHandler : PacketHandler<PlayerReadinessPacket>
{
    public void Send(PlayerReadinessState readyState, int progress = 0)
    {
        var packet = new PlayerReadinessPacket
        {
            readyState = readyState,
            progress = progress
        };

        RequestSend(packet);
    }

    protected override void WhenApproved(PlayerReadinessPacket packet, NetPeer peer)
    {
        if (H.Scoreboard == null) return;

        PlayerScore playerScore = H.GetPlayerScore(packet.player);
        if (playerScore == null) H.Scoreboard[packet.player.Id] = new PlayerScore(packet.player.Id);

        playerScore?.readyState = packet.readyState;

        if (FikaBackendUtils.IsServer)
        {
            // In case a player is reporting they are connected mid session (reconnects, new joins)
            if (H.Session?.matchState != MatchState.None && packet.readyState == PlayerReadinessState.Connected)
            {
                if (packet.player == null) return;

                if (!H.Scoreboard.ContainsKey(packet.player.Id))
                {
                    H.Scoreboard[packet.player.Id] = new PlayerScore(packet.player.Id);
                    H.GetPlayerScore(packet.player.Id).faction = Faction.Spectator;
                }

                Singleton<SessionStartPacketHandler>.Instance.SendToPlayer(packet.player);
                Singleton<SessionInfoPacketHandler>.Instance.SendToPlayer(packet.player);
            }
        }
        else
        {
            Singleton<AdminLoginPacketHandler>.Instance.Send();
        }
    }
}
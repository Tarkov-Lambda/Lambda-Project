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
public partial struct PlayerReadinessPacket : INetSerializable
{
    public int playerId;
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
            playerId = H.MainPlayer.Id,
            readyState = readyState,
            progress = progress
        };

        RequestSend(packet);
    }

    protected override void WhenApproved(PlayerReadinessPacket packet, NetPeer peer)
    {
        PlayerScore playerScore = H.GetPlayerScore(packet.playerId);
        if (playerScore == null) H.Scoreboard[packet.playerId] = new PlayerScore(packet.playerId);

        playerScore?.readyState = packet.readyState;

        if (FikaBackendUtils.IsServer)
        {
            // In case a player is reporting they are connected mid session (reconnects, new joins)
            if (H.Session?.matchState != MatchState.None && packet.readyState == PlayerReadinessState.Connected)
            {
                Player player = H.GetPlayer(packet.playerId);
                if (player == null) return;

                if (!H.Scoreboard.ContainsKey(packet.playerId))
                {
                    H.Scoreboard[packet.playerId] = new PlayerScore(packet.playerId);
                    H.GetPlayerScore(packet.playerId).faction = Faction.Spectator;
                }

                Singleton<SessionStartPacketHandler>.Instance.SendToPlayer(player);
                Singleton<SessionInfoPacketHandler>.Instance.SendToPlayer(player);
            }
        }
        else
        {
            Singleton<AdminLoginPacketHandler>.Instance.Send();
        }
    }
}
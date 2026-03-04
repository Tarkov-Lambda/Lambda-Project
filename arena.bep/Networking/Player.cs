using Comfort.Common;
using EFT;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.Core;
using ifp.arena.bep.Core.Gamemode;
using ifp.arena.bep.networking.Base;
using ifp.arena.shared;

namespace ifp.arena.bep.networking
{
    public struct PlayerKilledPacket : INetSerializable
    {
        public int killerId;
        public int victimId;
        public int assistId;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(killerId);
            writer.Put(victimId);
            writer.Put(assistId);
        }

        public void Deserialize(NetDataReader reader)
        {
            killerId = reader.GetInt();
            victimId = reader.GetInt();
            assistId = reader.GetInt();
        }

        public override string ToString()
        {
            return $"{killerId} killed {victimId}";
        }
    }

    public class PlayerKilledPacketHandler : PacketHandler<PlayerKilledPacket>
    {
        public void Send(int killerId, int victimId, int assistId)
        {
            var packet = new PlayerKilledPacket
            {
                killerId = killerId,
                victimId = victimId,
                assistId = assistId
            };

            RequestSend(packet);
        }

        public override void OnReceive(PlayerKilledPacket packet, NetPeer peer)
        {
            if (H.Scoreboard[packet.killerId] != null)
            {
                H.Scoreboard[packet.killerId].kills++;
            }

            if (H.Scoreboard[packet.victimId] != null)
            {
                H.Scoreboard[packet.victimId].deaths++;
            }

            if (H.Session.currentGameMode == GameModes.SND)
            {
                H.Scoreboard[packet.victimId].isAlive = false;
            }

            // Player position interpolation mitigation
            Player victim = H.GetPlayer(packet.victimId);
            if (victim != null && victim != H.MainPlayer)
            {
                H.GetPlayer(packet.victimId).Position = new UnityEngine.Vector3();
            }

            EventBus.OnPlayerKill(packet);
        }
    }

    public struct FactionChangePacket : INetSerializable
    {
        public int id;
        public Faction faction;


        public void Serialize(NetDataWriter writer)
        {
            writer.Put(id);
            writer.Put((int)faction);

        }

        public void Deserialize(NetDataReader reader)
        {
            id = reader.GetInt();
            faction = (Faction)reader.GetInt();
        }

        public override string ToString()
        {
            return $"{id} changed faction to {faction}";
        }
    }

    public class FactionChangePacketHandler : PacketHandler<FactionChangePacket>
    {
        public void Send(Faction faction)
        {
            var packet = new FactionChangePacket
            {
                id = H.GameWorld.MainPlayer.Id,
                faction = faction
            };

            RequestSend(packet);
        }

        public override void OnReceive(FactionChangePacket packet, NetPeer peer)
        {
            H.Scoreboard[packet.id].faction = packet.faction;
        }
    }
}
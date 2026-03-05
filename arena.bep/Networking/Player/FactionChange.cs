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
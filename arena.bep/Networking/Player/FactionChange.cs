using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.Core;
using ifp.arena.bep.networking.Base;
using ifp.arena.shared;
using MemoryPack;

namespace ifp.arena.bep.networking
{
    [MemoryPackable]
    public partial struct FactionChangePacket : INetSerializable
    {
        public int id;
        public Faction faction;

        public void Serialize(NetDataWriter writer) => MemoryPackHelper.Serialize(writer, this);
        public void Deserialize(NetDataReader reader) => this = MemoryPackHelper.Deserialize<FactionChangePacket>(reader);
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

        protected override void WhenApproved(FactionChangePacket packet, NetPeer peer)
        {
            if (H.Scoreboard[packet.id] != null)
            {
                H.Scoreboard[packet.id].faction = packet.faction;
            }
        }
    }
}

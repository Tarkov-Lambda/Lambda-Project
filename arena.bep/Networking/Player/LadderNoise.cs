using EFT;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.Core;
using ifp.arena.bep.networking.Base;
using MemoryPack;

namespace ifp.arena.bep.networking
{
    [MemoryPackable]
    public partial struct LadderNoisePacket : INetSerializable
    {
        public int id;

        public void Serialize(NetDataWriter writer) => MemoryPackHelper.Serialize(writer, this);
        public void Deserialize(NetDataReader reader) => this = MemoryPackHelper.Deserialize<LadderNoisePacket>(reader);
    }

    public class LadderNoisePacketHandler : PacketHandler<LadderNoisePacket>
    {
        public void Send()
        {
            RequestSend(new LadderNoisePacket { id = H.MainPlayer.Id });
        }

        protected override void LocalPredictApproved(LadderNoisePacket packet)
        {
            MakeLadderNoise(H.MainPlayer);
        }


        protected override void WhenApproved(LadderNoisePacket packet, NetPeer peer)
        {
            Player player = H.GetPlayer(packet.id);
            if (player.IsYourPlayer) return;

            MakeLadderNoise(player);
        }

        private void MakeLadderNoise(Player player)
        {
            D.Notify("Ladder Noise");
        }
    }
}

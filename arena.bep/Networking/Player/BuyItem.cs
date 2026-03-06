using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using Fika.Core.Main.Utils;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.Core;
using ifp.arena.bep.Core.Gamemode;
using ifp.arena.bep.GameTypes;
using ifp.arena.bep.networking.Base;

namespace ifp.arena.bep.networking
{
    public struct BuyItemPacket : INetSerializable
    {
        public int playerId;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(playerId);
        }

        public void Deserialize(NetDataReader reader)
        {
            playerId = reader.GetInt();
        }
    }

    public class BuyItemPacketHandler : PacketHandler<BuyItemPacket>
    {
        public void Send(Item item)
        {
            var packet = new BuyItemPacket
            {
                playerId = H.MainPlayer.Id,
            };

            RequestSend(packet);
        }

        // local client, server, remote clients all execute this when the packet receives them
        public override void OnReceive(BuyItemPacket packet, NetPeer peer)
        {
        }
    }
}

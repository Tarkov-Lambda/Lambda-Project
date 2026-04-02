using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using EFT;
using EFT.InventoryLogic;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using Fika.Core.Networking.Pooling;
using ifp.arena.bep.Core;
using ifp.arena.bep.networking.Base;
using ifp.arena.bep.networking.Base.RateLimiting;
using Newtonsoft.Json;

namespace ifp.arena.bep.networking
{
    public struct PopPacket : INetSerializable
    {
        public int playerId;
        public Item item;
        public ItemAddress itemAddress;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(playerId);
            writer.PutItem(item);
            writer.Put(itemAddress);
        }

        public void Deserialize(NetDataReader reader)
        {
            playerId = reader.GetInt();
            item = reader.GetItem();
            itemAddress = reader.GetItemAddress(H.GetPlayer(playerId));
        }
    }

    public class PopPacketHandler : PacketHandler<PopPacket>
    {

        protected override RateLimitConfig ServerRateLimit => new(
            enabled: true,
            refillPerSecond: 5,
            burst: 20,
            costPerPacket: 1,
            action: RateLimitAction.Reject,
            stateTtlSeconds: 60,
            rejectCooldownSeconds: 1.0);

        public void Send(Item item)
        {
            var packet = new PopPacket
            {
                playerId = H.MainPlayer.Id,
                item = item,
                itemAddress = item.CurrentAddress
            };

            RequestSend(packet);
        }

        protected override void LocalPredictApproved(PopPacket packet)
        {
            IU.TryPopItemWithoutRestriction(packet.item, packet.itemAddress, H.MainPlayer).Forget();
        }


        protected override async void WhenApproved(PopPacket packet, NetPeer peer)
        {
            Player player = H.GetPlayer(packet.playerId); 
            if (player.IsYourPlayer) return;
            IU.TryPopItemWithoutRestriction(packet.item, packet.itemAddress, player).Forget();
        }
    }
}

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
    public struct RefreshPlateAddressPacket : INetSerializable
    {
        public int playerId;
        public ItemAddress address;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(playerId);
            writer.Put(address);
        }

        public void Deserialize(NetDataReader reader)
        {
            playerId = reader.GetInt();
            address = reader.GetItemAddress(H.GetPlayer(playerId));
        }
    }

    public class RefreshPlateAddressPacketHandler : PacketHandler<RefreshPlateAddressPacket>
    {

        protected override RateLimitConfig ServerRateLimit => new(
            enabled: true,
            refillPerSecond: 5,
            burst: 20,
            costPerPacket: 1,
            action: RateLimitAction.Reject,
            stateTtlSeconds: 60,
            rejectCooldownSeconds: 1.0);

        public void Send(ItemAddress address)
        {
            var packet = new RefreshPlateAddressPacket
            {
                playerId = H.MainPlayer.Id,
                address = address
            };

            RequestSend(packet);
        }

        protected override async void WhenApproved(RefreshPlateAddressPacket packet, NetPeer peer)
        {
            Slot plateSlot = packet.address.Container as Slot;
            plateSlot.ApplyContainedItem();
        }
    }
}

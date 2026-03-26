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
    public struct SpawnItemPacket : INetSerializable
    {
        public int playerId;
        public ItemPlacement placement;
        public Item item;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(playerId);
            writer.Put(placement);
            writer.PutItem(item);
        }

        public void Deserialize(NetDataReader reader)
        {
            playerId = reader.GetInt();
            placement = reader.GetItemPlacement(H.GetPlayer(playerId));
            item = reader.GetItem();
        }
    }

    public class SpawnItemPacketHandler : PacketHandler<SpawnItemPacket>
    {
        private readonly Dictionary<int, UniTask> _chains = new();

        protected override RateLimitConfig ServerRateLimit => new(
            enabled: true,
            refillPerSecond: 5,
            burst: 20,
            costPerPacket: 1,
            action: RateLimitAction.Reject,
            stateTtlSeconds: 60,
            rejectCooldownSeconds: 1.0);

        public void Send(Item item, ItemPlacement placement)
        {
            var packet = new SpawnItemPacket
            {
                playerId = H.MainPlayer.Id,
                item = item,
                placement = placement
            };

            RequestSend(packet);
        }

        protected override async void WhenApproved(SpawnItemPacket packet, NetPeer peer)
        {
            Player player = H.GetPlayer(packet.playerId);
            
            UniTask prev = _chains.TryGetValue(packet.playerId, out var existing) ? existing : UniTask.CompletedTask;

            // even though we are in a chain, this doesn't stop the player from moving something in their inventory
            // can definitely cause major issues
            _chains[packet.playerId] = prev.ContinueWith(async () =>
            {
                try
                {
                    await IU.LoadBundlesForItem(packet.item);

                    packet.item.StackObjectsCount = 1;

                    await IU.WhenApprovedGiveItem(packet.item, player, packet.placement);
                }
                catch (Exception ex)
                {
                    D.Log($"[SpawnItem] Chain step failed for player {packet.playerId}");
                    D.Dump(ex);
                    // D.LogError(ex.StackTrace);
                }
            });
        }

        public new void Dispose()
        {
            _chains.Clear();
            base.Dispose();
        }
    }
}

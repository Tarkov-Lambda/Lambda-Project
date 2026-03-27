using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using EFT;
using EFT.InventoryLogic;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.Core;
using ifp.arena.bep.networking.Base;
using ifp.arena.bep.networking.Base.RateLimiting;

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
            writer.Put(item);
        }

        public void Deserialize(NetDataReader reader)
        {
            playerId = reader.GetInt();
            Player player = H.GetPlayer(playerId);
            // D.Log(player.Profile.Nickname);
            placement = reader.GetItemPlacement(player);
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

        protected override async void LocalPredictApproved(SpawnItemPacket packet)
        {
            SpawnItem(packet, H.MainPlayer);
        }

        protected override async void WhenApproved(SpawnItemPacket packet, NetPeer peer)
        {
            Player player = H.GetPlayer(packet.playerId);
            if (player.IsYourPlayer) return;

            SpawnItem(packet, player);
        }

        private async void SpawnItem(SpawnItemPacket packet, Player player)
        {
            // UniTask prev = _chains.TryGetValue(packet.playerId, out var existing) ? existing : UniTask.CompletedTask;

            // _chains[packet.playerId] = prev.ContinueWith(async () =>
            // {
            //     await UniTask.Delay(25);
            //     try
            //     {
                    await IU.LoadBundlesForItem(packet.item);
 
                    await IU.WhenApprovedGiveItem(packet.item, player, packet.placement);
            //     }
            //     catch (Exception ex)
            //     {
            //         D.Notify($"[SpawnItem] Chain step failed for player {player.Profile.Nickname}");
            //         D.Dump(ex);
            //         D.LogError(ex.StackTrace);
            //     }
            // });
        }

        public new void Dispose()
        {
            _chains.Clear();
            base.Dispose();
        }
    }
}

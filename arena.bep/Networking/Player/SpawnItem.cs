using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using EFT;
using EFT.InventoryLogic;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.Core;
using ifp.arena.bep.Core.Economy;
using ifp.arena.bep.networking.Base;
using ifp.arena.bep.networking.Base.RateLimiting;
using ifp.arena.shared.Models;

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

        // we have to blindly accept our packet here otherwise ItemPlacement is not aware
        // and tries to spawn multiple things in one grid
        // otherwise we have to rewrite the logic to make the server give us spawn item packages effectivelly (gun + mags, 2 armor plates)
        protected override async void LocalPredictApproved(SpawnItemPacket packet)
        {
            SpawnItem(packet, H.MainPlayer);
            // we already spend money locally before requesting to begin with.
        }

        protected override async void WhenApproved(SpawnItemPacket packet, NetPeer peer)
        {
            Player player = H.GetPlayer(packet.playerId);
            if (player.IsYourPlayer) return;
            SpawnItem(packet, player);

            if (BuyMenu.TryGetItemData(packet.item.TemplateId, out ShopItem itemData))
            {
                H.GetPlayerScore(player.Id).SpendMoney(itemData.price);
            }
        }

        private async void SpawnItem(SpawnItemPacket packet, Player player)
        {
            await IU.LoadBundlesForItem(packet.item);
            await IU.WhenApprovedGiveItem(packet.item, player, packet.placement);
        }

        public new void Dispose()
        {
            _chains.Clear();
            base.Dispose();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using EFT;
using EFT.InventoryLogic;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.Core;
using ifp.arena.bep.networking.Base;
using ifp.arena.bep.networking.Base.RateLimiting;
using Newtonsoft.Json;

namespace ifp.arena.bep.networking
{
    public struct SpawnItemPacket : INetSerializable
    {
        public int playerId;
        public FlatItemsDataClass[] flatItems;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(playerId);

            if (flatItems == null)
            {
                writer.Put(0);
                return;
            }

            writer.Put(flatItems.Length);

            foreach (var item in flatItems)
            {
                writer.Put(item._id.ToString());
                writer.Put(item._tpl.ToString());

                bool hasParent = item.parentId.HasValue;
                writer.Put(hasParent);
                if (hasParent)
                {
                    writer.Put(item.parentId.Value.ToString());
                }

                bool hasSlot = !string.IsNullOrEmpty(item.slotId);
                writer.Put(hasSlot);
                if (hasSlot)
                {
                    writer.Put(item.slotId);
                }

                bool hasLocation = item.location != null;
                writer.Put(hasLocation);
                if (hasLocation)
                {
                    writer.Put(JsonConvert.SerializeObject(item.location));
                }

                bool hasUpd = item.upd != null;
                writer.Put(hasUpd);
                if (hasUpd)
                {
                    writer.Put(JsonConvert.SerializeObject(item.upd));
                }
            }
        }

        public void Deserialize(NetDataReader reader)
        {
            playerId = reader.GetInt();

            int itemsCount = reader.GetInt();
            if (itemsCount == 0)
            {
                flatItems = new FlatItemsDataClass[0];
                return;
            }

            flatItems = new FlatItemsDataClass[itemsCount];
            for (int i = 0; i < itemsCount; i++)
            {
                var item = new FlatItemsDataClass();

                item._id = new MongoID(reader.GetString());
                item._tpl = new MongoID(reader.GetString());

                if (reader.GetBool())
                {
                    item.parentId = new MongoID(reader.GetString());
                }

                if (reader.GetBool())
                {
                    item.slotId = reader.GetString();
                }

                if (reader.GetBool())
                {
                    item.location = JsonConvert.DeserializeObject<GClass846>(reader.GetString());
                }

                if (reader.GetBool())
                {
                    item.upd = JsonConvert.DeserializeObject<GClass846>(reader.GetString());
                }

                flatItems[i] = item;
            }
        }
    }

    public class SpawnItemPacketHandler : PacketHandler<SpawnItemPacket>
    {
        // Tail of the sequential async chain per player.
        // New work is appended so items are placed one-at-a-time per player,
        // preventing concurrent WhenApproved calls from racing over the same slot.
        private readonly Dictionary<int, UniTask> _chains = new();

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
            var packet = new SpawnItemPacket
            {
                playerId = H.MainPlayer.Id,
                flatItems = FU.ItemFactory.TreeToFlatItems(item)
            };

            RequestSend(packet);
        }

        // local client, server, remote clients all execute this packet on arrival (synchronization of weapon generation)
        protected override void WhenApproved(SpawnItemPacket packet, NetPeer peer)
        {
            if (packet.flatItems == null || packet.flatItems.Length == 0) return;

            var itemStruct = FU.ItemFactory.FlatItemsToTree(packet.flatItems);
            Item rootItem = null;

            foreach (var flatItem in packet.flatItems)
            {
                if (!flatItem.parentId.HasValue || !itemStruct.Items.ContainsKey(flatItem.parentId.Value.ToString()))
                {
                    if (itemStruct.Items.TryGetValue(flatItem._id.ToString(), out var foundItem))
                    {
                        rootItem = foundItem;
                        break;
                    }
                }
            }

            if (rootItem == null) itemStruct.Items.TryGetValue(packet.flatItems[0]._id.ToString(), out rootItem);

            if (rootItem != null)
            {
                int playerId = packet.playerId;
                Item captured = rootItem;

                // Get the existing chain tail for this player, or start fresh
                UniTask prev = _chains.TryGetValue(playerId, out var existing)
                    ? existing
                    : UniTask.CompletedTask;

                // Append our work sequentially; each step waits for the previous to finish
                _chains[playerId] = prev.ContinueWith(async () =>
                {
                    try
                    {
                        await IU.LoadBundlesForItem(captured);
                        await IU.WhenApprovedGiveItem(captured, H.GetPlayer(playerId));
                    }
                    catch (Exception ex)
                    {
                        Plugin.Logger.LogWarning($"[SpawnItem] Chain step failed for player {playerId}: {ex.Message}");
                    }
                });
            }
        }

        public new void Dispose()
        {
            _chains.Clear();
            base.Dispose();
        }
    }
}

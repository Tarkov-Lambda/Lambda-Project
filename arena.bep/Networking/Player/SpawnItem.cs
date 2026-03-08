using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Comfort.Common;
using EFT;
using EFT.Interactive;
using EFT.InventoryLogic;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.Core;
using ifp.arena.bep.networking.Base;
using ifp.arena.bep.networking.Base.RateLimiting;
using Newtonsoft.Json;
using UnityEngine;
using static ItemFactoryClass;

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
        protected override RateLimitConfig ServerRateLimit => new(
            enabled: true,
            refillPerSecond: 1,
            burst: 10,
            costPerPacket: 1,
            action: RateLimitAction.Reject,
            stateTtlSeconds: 60,
            rejectCooldownSeconds: 1.0);

        public void Send(Item item)
        {
            var packet = new SpawnItemPacket
            {
                playerId = H.MainPlayer.Id,
                flatItems = ItemsUtils.ItemFactory.TreeToFlatItems(item)
            };

            RequestSend(packet);
        }

        // local client, server, remote clients all execute this packet on arrival (synchronization of weapon generation)
        public override void WhenApproved(SpawnItemPacket packet, NetPeer peer)
        {
            if (packet.flatItems == null || packet.flatItems.Length == 0) return;

            var itemStruct = ItemsUtils.ItemFactory.FlatItemsToTree(packet.flatItems);
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
                _ = LoadBundlesAndSpawnAsync(rootItem, packet.playerId);
            }
        }

        private async Task LoadBundlesAndSpawnAsync(Item rootItem, int playerId)
        {
            var prefabsToLoad = rootItem.GetAllItems()
                .Select(i => i.Template.Prefab)
                .Where(p => p != null && !string.IsNullOrEmpty(p.path))
                .ToList();

            if (prefabsToLoad.Count > 0)
            {
                await Singleton<PoolManagerClass>.Instance.LoadBundlesAndCreatePools(
                    PoolManagerClass.PoolsCategory.Raid,
                    PoolManagerClass.AssemblyType.Local, // Standard for local generation
                    prefabsToLoad,                       // ICollection<ResourceKey>
                    JobPriorityClass.Immediate,          // GDelegate62 (Yield logic)
                    null,                                // IProgress callback (null is fine)
                    default(CancellationToken)           // Cancellation token
                );
            }

            H.Notify(rootItem.LocalizedName());

            ItemsUtils.SyncGiveItem(rootItem, H.GetPlayer(playerId));
        }
    }
}

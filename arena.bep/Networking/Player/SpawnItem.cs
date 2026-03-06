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

            // Handle null arrays gracefully
            if (flatItems == null)
            {
                writer.Put(0);
                return;
            }

            // 1. Write the array length
            writer.Put(flatItems.Length);

            // 2. Iterate and write each item
            foreach (var item in flatItems)
            {
                // Serialize MongoIDs
                writer.Put(item._id.ToString());
                writer.Put(item._tpl.ToString());

                // Serialize nullable parentId
                bool hasParent = item.parentId.HasValue;
                writer.Put(hasParent);
                if (hasParent)
                {
                    writer.Put(item.parentId.Value.ToString());
                }

                // Serialize slotId
                bool hasSlot = !string.IsNullOrEmpty(item.slotId);
                writer.Put(hasSlot);
                if (hasSlot)
                {
                    writer.Put(item.slotId);
                }

                // Serialize location (GClass846)
                // We use JSON here because location descriptors can change and have arbitrary properties (x, y, r, isSearched)
                bool hasLocation = item.location != null;
                writer.Put(hasLocation);
                if (hasLocation)
                {
                    writer.Put(JsonConvert.SerializeObject(item.location));
                }

                // Serialize upd (GClass846)
                // We use JSON here because Upd is deeply polymorphic. Manual serialization would break every EFT update.
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

            // 1. Read the array length
            int itemsCount = reader.GetInt();
            if (itemsCount == 0)
            {
                flatItems = new FlatItemsDataClass[0];
                return;
            }

            // 2. Iterate and read each item
            flatItems = new FlatItemsDataClass[itemsCount];
            for (int i = 0; i < itemsCount; i++)
            {
                var item = new FlatItemsDataClass();

                // Deserialize MongoIDs (using EFT's native constructor that accepts strings)
                item._id = new MongoID(reader.GetString());
                item._tpl = new MongoID(reader.GetString());

                // Deserialize parentId
                if (reader.GetBool())
                {
                    item.parentId = new MongoID(reader.GetString());
                }

                // Deserialize slotId
                if (reader.GetBool())
                {
                    item.slotId = reader.GetString();
                }

                // Deserialize location (GClass846)
                if (reader.GetBool())
                {
                    item.location = JsonConvert.DeserializeObject<GClass846>(reader.GetString());
                }

                // Deserialize upd (GClass846)
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
        public void Send(Item item)
        {
            var packet = new SpawnItemPacket
            {
                playerId = H.MainPlayer.Id,
                flatItems = PresetUtils.ItemFactory.TreeToFlatItems(item)
            };

            RequestSend(packet);
        }

        // local client, server, remote clients all execute this packet on arrival (synchronization of weapon generation)

        public override void WhenApproved(SpawnItemPacket packet, NetPeer peer)
        {
            if (packet.flatItems == null || packet.flatItems.Length == 0) return;

            var itemStruct = PresetUtils.ItemFactory.FlatItemsToTree(packet.flatItems);
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
                // Network packets arrive synchronously.
                // Fire-and-forget a Task so we don't block the networking thread while Tarkov loads 3D models.
                _ = LoadBundlesAndSpawnAsync(rootItem, packet.playerId);
            }
        }

        private async Task LoadBundlesAndSpawnAsync(Item rootItem, int playerId)
        {
            // 1. Gather all required Asset Bundles (Prefabs) for the root item AND all its nested children
            // GetAllItems() is an EFT method that traverses the entire item tree.
            var prefabsToLoad = rootItem.GetAllItems()
                .Select(i => i.Template.Prefab) // Extracts the ResourceKey from the item template
                .Where(p => p != null && !string.IsNullOrEmpty(p.path))
                .ToList();

            // 2. Ask Tarkov's PoolManagerClass to load these bundles into RAM
            if (prefabsToLoad.Count > 0)
            {
                Plugin.Logger.LogInfo($"Loading {prefabsToLoad.Count} bundles for {rootItem.Name.Localized()}...");

                await Singleton<PoolManagerClass>.Instance.LoadBundlesAndCreatePools(
                    PoolManagerClass.PoolsCategory.Raid,
                    PoolManagerClass.AssemblyType.Local, // Standard for local generation
                    prefabsToLoad,                       // ICollection<ResourceKey>
                    JobPriorityClass.Immediate,          // GDelegate62 (Yield logic)
                    null,                                // IProgress callback (null is fine)
                    default(CancellationToken)           // Cancellation token
                );
            }

            Plugin.Logger.LogInfo($"Bundles loaded successfully! Processing spawn for Player ID: {playerId}");

            // 3. Spawning the Item

            // --> OPTION A: Give it directly to the player's inventory
            // Uncomment this if you want it to appear in their stash/hands
            PresetUtils.GiveItem(rootItem, H.GetPlayer(playerId)); 
        }
    }
}

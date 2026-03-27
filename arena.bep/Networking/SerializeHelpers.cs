using System;
using EFT;
using EFT.InventoryLogic;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib.Utils;
using Fika.Core.Networking.Pooling;
using ifp.arena.bep.Core;
using MemoryPack;
using Newtonsoft.Json;
using UnityEngine;

namespace ifp.arena.bep.networking
{
    public static class MemoryPackHelper
    {
        public static void Serialize<T>(NetDataWriter writer, T value)
        {
            writer.Put(MemoryPackSerializer.Serialize(value));
        }

        public static T Deserialize<T>(NetDataReader reader) where T : struct
        {
            int length = reader.AvailableBytes;
            byte[] bytes = new byte[length];
            reader.GetBytes(bytes, length);
            return MemoryPackSerializer.Deserialize<T>(bytes);
        }
    }

    public static class NetExtensions
    {
        public static void Put(this NetDataWriter writer, Vector3 v)
        {
            writer.Put(v.x);
            writer.Put(v.y);
            writer.Put(v.z);
        }

        public static Vector3 GetVector3(this NetDataReader reader)
        {
            return new Vector3(
                reader.GetFloat(),
                reader.GetFloat(),
                reader.GetFloat()
            );
        }

        // public static void Put(this NetDataWriter writer, Item item)
        // {
        //     FlatItemsDataClass[] _flatItems = FU.ItemFactory.TreeToFlatItems(item);
        //     writer.Put(_flatItems.Length);

        //     foreach (var itemInArray in _flatItems)
        //     {
        //         writer.Put(itemInArray._id.ToString());
        //         writer.Put(itemInArray._tpl.ToString());

        //         bool hasParent = itemInArray.parentId.HasValue;
        //         writer.Put(hasParent);
        //         if (hasParent)
        //         {
        //             writer.Put(itemInArray.parentId.Value.ToString());
        //         }

        //         bool hasSlot = !string.IsNullOrEmpty(itemInArray.slotId);
        //         writer.Put(hasSlot);
        //         if (hasSlot)
        //         {
        //             writer.Put(itemInArray.slotId);
        //         }

        //         bool hasLocation = itemInArray.location != null;
        //         writer.Put(hasLocation);
        //         if (hasLocation)
        //         {
        //             writer.Put(JsonConvert.SerializeObject(itemInArray.location));
        //         }

        //         bool hasUpd = itemInArray.upd != null;
        //         writer.Put(hasUpd);
        //         if (hasUpd)
        //         {
        //             writer.Put(JsonConvert.SerializeObject(itemInArray.upd));
        //         }
        //     }
        // }

        // public static Item GetItem(this NetDataReader reader)
        // {
        //     FlatItemsDataClass[] _flatItems;

        //     Item item = null;

        //     int itemsCount = reader.GetInt();

        //     _flatItems = new FlatItemsDataClass[itemsCount];
        //     for (int i = 0; i < itemsCount; i++)
        //     {
        //         var itemInArray = new FlatItemsDataClass
        //         {
        //             _id = new MongoID(reader.GetString()),
        //             _tpl = new MongoID(reader.GetString())
        //         };

        //         if (reader.GetBool())
        //         {
        //             itemInArray.parentId = new MongoID(reader.GetString());
        //         }

        //         if (reader.GetBool())
        //         {
        //             itemInArray.slotId = reader.GetString();
        //         }

        //         if (reader.GetBool())
        //         {
        //             itemInArray.location = JsonConvert.DeserializeObject<GClass846>(reader.GetString());
        //         }

        //         if (reader.GetBool())
        //         {
        //             itemInArray.upd = JsonConvert.DeserializeObject<GClass846>(reader.GetString());
        //         }

        //         _flatItems[i] = itemInArray;
        //     }



        //     var itemStruct = FU.ItemFactory.FlatItemsToTree(_flatItems);

        //     foreach (var flatItem in _flatItems)
        //     {
        //         if (!flatItem.parentId.HasValue || !itemStruct.Items.ContainsKey(flatItem.parentId.Value.ToString()))
        //         {
        //             if (itemStruct.Items.TryGetValue(flatItem._id.ToString(), out var foundItem))
        //             {
        //                 item = foundItem;
        //                 break;
        //             }
        //         }
        //     }

        //     if (item == null) itemStruct.Items.TryGetValue(_flatItems[0]._id.ToString(), out item);

        //     return item;
        // }

        public static void Put(this NetDataWriter writer, ItemPlacement placement)
        {
            writer.Put((int)placement.Slot);
            writer.Put((int)placement.Kind);

            writer.Put(placement.Address);
        }

        public static ItemPlacement GetItemPlacement(this NetDataReader reader, Player player)
        {
            var placementSlot = (EquipmentSlot)reader.GetInt();
            var placementKind = (PlacementKind)reader.GetInt();
            ItemAddress address = reader.GetItemAddress(player);

            return new ItemPlacement(placementKind, placementSlot, address);
        }

        public static void Put(this NetDataWriter writer, ItemAddress itemAddress)
        {
            GClass1950 descriptor = itemAddress.ToDescriptor();
            EFTWriterClass eftWriter = WriterPoolManager.GetWriter();
            eftWriter.WritePolymorph(descriptor);
            byte[] _addressDescriptor = eftWriter.ToArray();
            WriterPoolManager.ReturnWriter(eftWriter);
            writer.PutByteArray(_addressDescriptor);
        }
        
        public static ItemAddress GetItemAddress(this NetDataReader reader, Player player)
        {
            byte[] _addressDescriptor = reader.GetByteArray();
            using var eftReader = PacketToEFTReaderAbstractClass.Get(_addressDescriptor);
            var descriptor = eftReader.ReadPolymorph<GClass1950>();
            return player.InventoryController.ToItemAddress(descriptor);
        }

        public static void Put(this NetDataWriter writer, Quaternion q)
        {
            writer.Put(q.x);
            writer.Put(q.y);
            writer.Put(q.z);
            writer.Put(q.w);
        }

        public static Quaternion GetQuaternion(this NetDataReader reader)
        {
            return new Quaternion(
                reader.GetFloat(),
                reader.GetFloat(),
                reader.GetFloat(),
                reader.GetFloat()
            );
        }

    }
}

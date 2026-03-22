using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EFT.InventoryLogic;
using Fika.Core.Networking.LiteNetLib.Utils;
using MemoryPack;
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

        // IContainer.ID is defined on the interface in the game assembly but is absent from the
        // compile-time reference/stub. We access it via reflection so the mod compiles and works at runtime.
        private static readonly PropertyInfo _containerIdProp =
            typeof(EFT.InventoryLogic.IContainer).GetProperty("ID", BindingFlags.Public | BindingFlags.Instance);

        // GClass1954 (concrete grid descriptor) exposes LocationInGrid which holds x, y, and rotation.
        // We reflect on it at serialize-time to extract the location without needing a GClass1954 reference.
        private static readonly PropertyInfo _locationInGridProp =
            Type.GetType("GClass1954, Assembly-CSharp")?.GetProperty("LocationInGrid", BindingFlags.Public | BindingFlags.Instance);

        private static string GetContainerId(EFT.InventoryLogic.IContainer container) =>
            _containerIdProp?.GetValue(container) as string ?? string.Empty;

        /// <summary>
        /// Serializes an <see cref="ItemAddress"/> as a compact binary record:
        /// <c>[containerId: string] [isGrid: bool] [x: int, y: int, r: int]</c> (x/y/r only for grids).
        /// <para>
        /// For <see cref="StashGridClass"/> containers the location is extracted from the
        /// <c>GClass1954</c> descriptor returned by <see cref="ItemAddress.ToDescriptor()"/>.
        /// For <see cref="Slot"/> containers no location data is needed.
        /// </para>
        /// </summary>
        public static void Put(this NetDataWriter writer, ItemAddress address)
        {
            bool isGrid = address.Container is StashGridClass;
            writer.Put(GetContainerId(address.Container));
            writer.Put(isGrid);
            if (isGrid)
            {
                var descriptor = address.ToDescriptor();
                var location = _locationInGridProp?.GetValue(descriptor) as LocationInGrid;
                writer.Put(location?.x ?? 0);
                writer.Put(location?.y ?? 0);
                writer.Put((int)(location?.r ?? ItemRotation.Horizontal));
            }
        }

        /// <summary>
        /// Reconstructs an <see cref="ItemAddress"/> from wire values
        /// (used in <c>WhenApproved</c> after <c>Deserialize</c> has stored the raw fields).
        /// <para>
        /// For <see cref="StashGridClass"/> containers calls <c>grid.CreateItemAddress(location)</c> directly.
        /// For <see cref="Slot"/> containers calls <c>slot.CreateItemAddress()</c> directly.
        /// No JSON or additional reflection is required on the receive side.
        /// </para>
        /// </summary>
        public static ItemAddress GetItemAddress(
            string containerId,
            bool isGrid,
            int locationX,
            int locationY,
            ItemRotation locationR,
            IEnumerable<EFT.InventoryLogic.IContainer> containers)
        {
            EFT.InventoryLogic.IContainer container = containers.FirstOrDefault(c => GetContainerId(c) == containerId);
            if (container == null) return null;

            if (isGrid && container is StashGridClass grid)
                return grid.CreateItemAddress(new LocationInGrid(locationX, locationY, locationR));

            if (!isGrid && container is Slot slot)
                return slot.CreateItemAddress();

            return null;
        }
    }
}

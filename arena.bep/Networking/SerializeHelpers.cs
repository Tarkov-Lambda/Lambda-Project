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
    }
}

using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.Core;
using ifp.arena.bep.networking.Base;
using ifp.arena.bep.networking.Base.RateLimiting;

namespace ifp.arena.bep.networking
{
    public struct AdminLoginPacket : INetSerializable
    {
        public int id;
        public string password;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(id);
            writer.Put(password);
        }

        public void Deserialize(NetDataReader reader)
        {
            id = reader.GetInt();
            password = reader.GetString();
        }
    }

    public class AdminLoginPacketHandler : PacketHandler<AdminLoginPacket>
    {
        public static RateLimitConfig AdminLogin => new(
            enabled: true,
            refillPerSecond: 1.0,
            burst: 3,
            costPerPacket: 1,
            action: RateLimitAction.Reject,
            stateTtlSeconds: 60,
            rejectCooldownSeconds: 1
        );

        public void Send()
        {
            var password = Plugin.MapName.Value;
            if (password == "") return;
            
            var packet = new AdminLoginPacket
            {
                id = H.MainPlayer.Id,
                password = Plugin.MapName.Value,
            };

            RequestSend(packet);
        }

        public override bool ServerValidation(ref AdminLoginPacket packet, NetPeer netPeer)
        {
            if (packet.password == Plugin.Password.Value)
            {
                packet.password = "";
                return true;
            }
            else
            {
                packet.password = "";
                return false;
            }
        }



        public override void WhenApproved(AdminLoginPacket packet, NetPeer peer)
        {
            H.GetPlayerScore(packet.id).isAdmin = true;
            H.Notify($"{H.GetPlayer(packet.id).name} is an Admin");
        }
    }
}
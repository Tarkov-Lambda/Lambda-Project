using Comfort.Common;
using EFT;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.GameTypes;
using ifp.arena.bep.networking.Base;
using ifp.arena.shared;
using System;
using System.Linq;

namespace ifp.arena.bep.networking
{
    public struct WeatherAndTimePacket : INetSerializable
    {
        public double minutesSinceMidnight;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(minutesSinceMidnight);
        }

        public void Deserialize(NetDataReader reader)
        {
            minutesSinceMidnight = reader.GetDouble();
        }

        public override string ToString()
        {
            return $"{minutesSinceMidnight}";
        }
    }

    public class WeatherAndTimePacketHandler : PacketHandler<WeatherAndTimePacket>
    {
        public void Send(int id)
        {
            var packet = new WeatherAndTimePacket
            {
                minutesSinceMidnight = id,
            };

            RequestSend(packet);
        }

        public override void OnReceive(WeatherAndTimePacket packet, NetPeer peer)
        {
            // NotificationManagerClass.DisplayMessageNotification($"{packet}");

            DateTime currentDateTime = Singleton<GameWorld>.Instance.GameDateTime.Calculate();
            DateTime modifiedDateTime = currentDateTime.Date + TimeSpan.FromMinutes(packet.minutesSinceMidnight);

            Singleton<GameWorld>.Instance.GameDateTime.Reset(modifiedDateTime);
        }
    }
}

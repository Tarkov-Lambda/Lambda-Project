using Comfort.Common;
using EFT;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.Core;
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
        public WeatherAndTimePacketHandler() : base(DeliveryMethod.ReliableOrdered, PacketAuthority.ServerOnly) { }

        public void Send(int id)
        {
            var packet = new WeatherAndTimePacket
            {
                minutesSinceMidnight = id,
            };

            RequestSend(packet);
        }

        public override void WhenApproved(WeatherAndTimePacket packet, NetPeer peer)
        {
            DateTime currentDateTime = H.GameWorld.GameDateTime.Calculate();
            DateTime modifiedDateTime = currentDateTime.Date + TimeSpan.FromMinutes(packet.minutesSinceMidnight);

            H.GameWorld.GameDateTime.Reset(modifiedDateTime);
        }
    }

    public static class TimeOfDayHelper
    {
        private const double START_MINUTES = 8 * 60;
        private const double END_MINUTES = 23 * 60;

        public static double GetMinutesForRound(int roundIndex, int maxRounds)
        {
            if (maxRounds <= 1)
                return START_MINUTES;

            double t = (double)roundIndex / (maxRounds - 1);
            return START_MINUTES + (END_MINUTES - START_MINUTES) * t;
        }
    }
}

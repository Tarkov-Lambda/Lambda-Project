using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using PacketHandler;
using MemoryPack;
using System;

namespace ifp.arena.bep.networking;

[MemoryPackable]
public partial struct WeatherAndTimePacket : INetSerializable
{
    public double minutesSinceMidnight;

    public void Serialize(NetDataWriter writer) => MemoryPackWrapper.Serialize(writer, this);
    public void Deserialize(NetDataReader reader) => this = MemoryPackWrapper.Deserialize<WeatherAndTimePacket>(reader);
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

        DispatchPacket(packet);
    }

    protected override void WhenApproved(WeatherAndTimePacket packet, NetPeer peer)
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
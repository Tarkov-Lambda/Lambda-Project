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

public class WeatherAndTimeSyncPacketHandler : PacketHandler<WeatherAndTimePacket>
{
    public WeatherAndTimeSyncPacketHandler() : base(DeliveryMethod.ReliableOrdered, PacketAuthority.ServerOnly) { }

    public void Send(double minutesSinceMidnight)
    {
        var packet = new WeatherAndTimePacket
        {
            minutesSinceMidnight = minutesSinceMidnight,
        };

        DispatchPacket(packet);
    }

    protected override void Apply(WeatherAndTimePacket packet, NetPeer peer)
    {
        DateTime currentDateTime = H.GameWorld.GameDateTime.Calculate();
        DateTime modifiedDateTime = currentDateTime.Date + TimeSpan.FromMinutes(packet.minutesSinceMidnight);

        H.GameWorld.GameDateTime.Reset(modifiedDateTime);
    }
}

public static class TimeOfDayHelper
{
    private const double DAY_START = 7 * 60;   // 07:00
    private const double DAY_END = 18 * 60;    // 18:00

    private const double NIGHT_START = 0;      // 00:00
    private const double NIGHT_END = 3 * 60;   // 03:00

    public static double GetMinutesForRound(int roundIndex, int maxRounds)
    {
        if (roundIndex < 9)
        {
            double t = roundIndex / 8.0; // spread across 9 rounds
            return DAY_START + (DAY_END - DAY_START) * t;
        }
        else
        {
            int nightIndex = roundIndex - 9; // 0–2
            double t = nightIndex / 2.0;
            return NIGHT_START + (NIGHT_END - NIGHT_START) * t;
        }
    }
}
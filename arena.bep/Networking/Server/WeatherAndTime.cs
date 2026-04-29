using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using PacketHandler;
using MemoryPack;
using System;
using UnityEngine;
using EFT.Weather;

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
        var fixedTime = new DateTime(2026, 4, 26, 0, 0, 0, DateTimeKind.Utc).AddMinutes(packet.minutesSinceMidnight);
        H.GameWorld.GameDateTime.Reset(fixedTime);

        try
        {
            var weatherController = GameObject.Find("Weather").GetComponent<WeatherController>();

            weatherController.WeatherDebug.Enabled = true;
            weatherController.WeatherDebug.CloudDensity = 0f;
            weatherController.WeatherDebug.Fog = 0f;
            weatherController.WeatherDebug.LightningThunderProbability = 0f;
            weatherController.WeatherDebug.Rain = 0f;
            weatherController.WeatherDebug.WindDirection = WeatherDebug.Direction.NW;
            weatherController.WeatherDebug.WindMagnitude = 0f;

            H.GameWorld.GameDateTime.TimeFactor = 0f;
        }
        catch { }
    }
}

public static class TimeOfDayHelper
{
    private const double DAY_START = 15 * 60;
    private const double DAY_END = 18 * 60;

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
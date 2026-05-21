using PacketWarden;
using MemoryPack;
using System;
using UnityEngine;
using EFT.Weather;

namespace Lambda.Core.Networking;

[MemoryPackable]
public partial struct WeatherAndTimePacket : IPacket
{
    public double minutesSinceMidnight;
}

public class WeatherAndTimeSyncPacketWarden : LambdaPacketWarden<WeatherAndTimePacket>
{
    protected override PacketAuthority Authority => PacketAuthority.ServerOnly;

    public void Send(double minutesSinceMidnight)
    {
        var packet = new WeatherAndTimePacket
        {
            minutesSinceMidnight = minutesSinceMidnight,
        };

        DispatchPacket(ref packet);
    }

    protected override void Apply(WeatherAndTimePacket packet, int peerId)
    {
        try
        {
            var fixedTime = new DateTime(2026, 6, 7, 0, 0, 0, DateTimeKind.Utc).AddMinutes(packet.minutesSinceMidnight);

            H.GameWorld.GameDateTime.Reset(fixedTime);
            H.GameWorld.GameDateTime.TimeFactor = 0f;

            var todSky = TOD_Sky.Instance;
            if (todSky?.CurrentTime?.GameDateTime != null)
            {
                todSky.CurrentTime.GameDateTime.Reset(fixedTime);
                todSky.CurrentTime.GameDateTime.TimeFactor = 0f;
            }

            var controller = WeatherController.Instance;
            controller.WeatherDebug.Enabled = true;
            controller.WeatherDebug.CloudDensity = 0.1f;
            controller.WeatherDebug.Fog = 0f;
            controller.WeatherDebug.LightningThunderProbability = 100f;
            controller.WeatherDebug.Rain = 0f;
            controller.WeatherDebug.WindDirection = WeatherDebug.Direction.NW;
            controller.WeatherDebug.WindMagnitude = 0f;
        }
        catch (Exception ex)
        {
            D.LogError($"Failed to apply packet: {ex}");
        }
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
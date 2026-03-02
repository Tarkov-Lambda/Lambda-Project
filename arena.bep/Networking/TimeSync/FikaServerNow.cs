using System;
using HarmonyLib;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib;

namespace ifp.arena.bep.networking.TimeSync
{
    internal static class ServerUtcClock
    {
        // Smoothed offset (ticks) that we add to DateTime.UtcNow
        private static double _offsetTicks;

        // Tune this: 0.1 = fairly smooth, 1.0 = no smoothing
        private const double Alpha = 0.2;

        public static DateTime UtcNow
        {
            get
            {
                var ticks = DateTime.UtcNow.Ticks + (long)_offsetTicks;
                return new DateTime(ticks, DateTimeKind.Utc);
            }
        }

        public static void UpdateFromPeer(NetPeer peer)
        {
            // NetPeer inherits LiteNetPeer, so RemoteTimeDelta/RemoteUtcTime exist
            var newOffset = (double)peer.RemoteTimeDelta;
            _offsetTicks = (_offsetTicks * (1.0 - Alpha)) + (newOffset * Alpha);
        }
    }
}

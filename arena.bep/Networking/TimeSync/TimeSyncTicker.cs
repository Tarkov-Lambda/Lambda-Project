using Comfort.Common;
using Fika.Core.Main.Utils;
using UnityEngine;

namespace ifp.arena.bep.networking.TimeSync
{
    /// <summary>
    /// Client-side Unity ticker that periodically sends timesync requests.
    /// </summary>
    public class TimeSyncTicker : MonoBehaviour
    {
        private double _nextSendLocalSeconds;

        // Requested cadence: 1 second
        private const double IntervalSeconds = 1.0;

        private void Awake()
        {
            _nextSendLocalSeconds = NetworkTime.LocalNowSeconds;
        }

        private void Update()
        {
            if (!Plugin.Active.Value)
                return;

            // Dedicated server / host server shouldn't send requests.
            if (FikaBackendUtils.IsServer)
                return;

            double now = NetworkTime.LocalNowSeconds;
            if (now < _nextSendLocalSeconds)
                return;

            _nextSendLocalSeconds = now + IntervalSeconds;

            if (!Singleton<TimeSyncRequestPacketHandler>.Instantiated)
                return;

            Singleton<TimeSyncRequestPacketHandler>.Instance.Send();
        }

        // Helpful when hot-reloading / restarting sessions without destroying DontDestroyOnLoad objects.
        private void OnDestroy()
        {
            NetworkTime.Reset();
        }
    }
}

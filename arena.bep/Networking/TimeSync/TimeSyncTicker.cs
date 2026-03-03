using Comfort.Common;
using EFT;
using Fika.Core.Main.Utils;
using UnityEngine;

namespace ifp.arena.bep.networking.TimeSync
{
    public class TimeSyncTicker : MonoBehaviour
    {
        private double _nextSendLocalSeconds;

        private const double IntervalSeconds = 1.0;

        private void Awake()
        {
            _nextSendLocalSeconds = NetworkTime.LocalNowSeconds;
        }

        private void Update()
        {
            if (!Plugin.Active.Value)
                return;

            if (FikaBackendUtils.IsServer)
                return;

            double now = NetworkTime.LocalNowSeconds;
            if (now < _nextSendLocalSeconds)
                return;

            _nextSendLocalSeconds = now + IntervalSeconds;

            if (!Singleton<TimeSyncRequestPacketHandler>.Instantiated)
                return;

            if (Singleton<GameWorld>.Instance == null)
                return;

            Singleton<TimeSyncRequestPacketHandler>.Instance.Send();
        }

        private void OnDestroy()
        {
            NetworkTime.Reset();
        }
    }
}

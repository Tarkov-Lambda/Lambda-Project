using Comfort.Common;
using Fika.Core.Main.Utils;
using ifp.arena.bep.networking.TimeSync;
using System.Collections.Generic;
using UnityEngine;

namespace ifp.arena.bep.networking
{
    /// <summary>
    /// - Server: sends physics snapshots at a fixed rate.
    /// - Client: interpolates received snapshots each physics tick.
    /// </summary>
    public class PhysicsSyncTicker : MonoBehaviour
    {
        // Desired snapshot rate (Hz)
        private const float SnapshotRateHz = 20f;
        private const float SnapshotInterval = 1f / SnapshotRateHz;

        // How far behind server time to sample for interpolation.
        private const double MinInterpBackTime = 0.10;

        // Max states per packet to stay under LiteNetLib's ~1020-byte limit for Unreliable/Sequenced.
        //
        // Rough sizing (no compression):
        //  netId (4) + pos (12) + rot (16) + vel (12) + angVel (12) + flags (1) = 57 bytes / object
        // plus packet header ~ (8+2+1) = 11 bytes.
        //  17 objects ~= 980 bytes, but safer to stay around 12-14.
        private const int MaxStatesPerPacket = 12;

        private float _nextServerSendTime;
        private ushort _serverTick;

        private readonly List<NetworkedPhysicsObject> _scratch = new(MaxStatesPerPacket);

        // Round-robin scan so that if there are more dirty objects than we can fit,
        // we don't starve objects earlier in the list.
        private int _serverScanCursor;

        private void Awake()
        {
            _nextServerSendTime = Time.time;
            _serverTick = 0;
            _serverScanCursor = 0;
        }

        private void FixedUpdate()
        {
            if (!Plugin.Active.Value)
                return;

            // Listen host: authoritative sim and sending snapshots.
            if (FikaBackendUtils.IsServer)
            {
                ServerFixedUpdate();
            }
            else
            {
                ClientFixedUpdate();
            }
        }

        private void ServerFixedUpdate()
        {
            if (Time.time < _nextServerSendTime)
                return;

            _nextServerSendTime = Time.time + SnapshotInterval;
            _serverTick++;

            if (!Singleton<PhysicsSnapshotPacketHandler>.Instantiated)
                return;

            double nowServer = NetworkTime.LocalNowSeconds;

            var all = NetworkedPhysicsObject.All;
            int total = all.Count;
            if (total <= 0)
                return;

            // We may need to send multiple packets this tick if lots of objects are dirty,
            // but we hard-cap packets per tick to avoid a bandwidth spike.
            const int MaxPacketsPerTick = 4;
            int packetsSent = 0;

            while (packetsSent < MaxPacketsPerTick)
            {
                _scratch.Clear();

                int scanned = 0;
                while (scanned < total && _scratch.Count < MaxStatesPerPacket)
                {
                    if (_serverScanCursor >= total)
                        _serverScanCursor = 0;

                    var obj = all[_serverScanCursor++];
                    scanned++;

                    if (obj == null || !obj.IsInitialized)
                        continue;

                    if (obj.ShouldSendServer(nowServer))
                        _scratch.Add(obj);
                }

                if (_scratch.Count == 0)
                    break;

                var packet = new PhysicsSnapshotPacket
                {
                    serverTimeSeconds = nowServer,
                    serverTick = _serverTick,
                    count = (byte)_scratch.Count,
                    states = new PhysicsSnapshotPacket.State[_scratch.Count]
                };

                for (int i = 0; i < _scratch.Count; i++)
                {
                    var obj = _scratch[i];
                    var rb = obj.Rigidbody;
                    packet.states[i] = new PhysicsSnapshotPacket.State
                    {
                        netId = obj.NetId,
                        position = rb.position,
                        rotation = rb.rotation,
                        velocity = rb.velocity,
                        angularVelocity = rb.angularVelocity,
                        flags = (byte)obj.GetCurrentFlags()
                    };

                    obj.MarkSentServer(nowServer);
                }

                Singleton<PhysicsSnapshotPacketHandler>.Instance.SendSnapshot(packet);
                packetsSent++;
            }
        }

        private void ClientFixedUpdate()
        {
            // Compute interpolation target in server time.
            double rtt = NetworkTime.EstimatedRttSeconds;
            double backTime = System.Math.Max(MinInterpBackTime, rtt * 2.0);
            double targetServerTime = NetworkTime.ServerNowSeconds - backTime;

            var all = NetworkedPhysicsObject.All;
            for (int i = 0; i < all.Count; i++)
            {
                var obj = all[i];
                if (obj == null || !obj.IsInitialized)
                    continue;

                obj.ClientFixedUpdate(targetServerTime);
            }
        }
    }
}

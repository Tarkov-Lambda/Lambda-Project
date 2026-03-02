using Comfort.Common;
using EFT;
using Fika.Core.Main.Utils;
using ifp.arena.bep;
using ifp.arena.bep.networking.TimeSync;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ifp.arena.bep.networking
{
    public class NetworkedPhysicsObject : MonoBehaviour
    {
        [Flags]
        public enum SnapshotFlags : byte
        {
            None = 0,
            Sleeping = 1 << 0,
        }

        private struct Snapshot
        {
            public double serverTime;
            public Vector3 position;
            public Quaternion rotation;
            public Vector3 velocity;
            public Vector3 angularVelocity;
            public SnapshotFlags flags;
        }

        // ------------ Static registry ------------
        private static readonly Dictionary<uint, NetworkedPhysicsObject> _byId = new();
        private static readonly List<NetworkedPhysicsObject> _all = new();

        public static bool TryGet(uint netId, out NetworkedPhysicsObject obj) => _byId.TryGetValue(netId, out obj);
        public static IReadOnlyList<NetworkedPhysicsObject> All => _all;

        // ------------ Identity ------------
        public string PathId { get; private set; }
        public uint NetId { get; private set; }
        public Rigidbody Rigidbody { get; private set; }

        // ------------ Server send state ------------
        private Vector3 _lastSentPos;
        private Quaternion _lastSentRot;
        private Vector3 _lastSentVel;
        private Vector3 _lastSentAngVel;
        private double _lastSentServerTime;
        private double _spawnServerTime;

        // ------------ Client snapshot buffer ------------
        private const int SnapshotBufferSize = 32;
        private readonly Snapshot[] _snapshots = new Snapshot[SnapshotBufferSize];
        private int _snapshotCount;
        private int _snapshotHead;

        // Latest authoritative sample (for prediction reconciliation)
        private bool _hasLatestAuth;
        private Snapshot _latestAuth;

        // ------------ Client prediction (local pushes only) ------------
        private float _predictionEndLocalTime;
        private const float PredictionWindowSeconds = 0.35f;
        private const float HardSnapDistance = 0.75f;
        private const float HardSnapAngleDeg = 30f;

        // ------------ Tuning ------------
        // Dirty thresholds (server)
        public const float PosEpsilon = 0.02f;          // meters
        public const float RotEpsilonDeg = 1.0f;       // degrees
        public const float VelEpsilon = 0.05f;         // m/s
        public const float AngVelEpsilon = 0.10f;      // rad/s
        public const double KeepAliveSeconds = 1.0;
        public const double AlwaysSendAfterSpawnSeconds = 2.0;

        public bool IsInitialized => NetId != 0 && !string.IsNullOrEmpty(PathId);

        public void Initialize(string hierarchyPath)
        {
            if (IsInitialized)
                return;

            PathId = hierarchyPath ?? string.Empty;
            NetId = Hash32(PathId);
            Rigidbody = GetComponent<Rigidbody>();

            if (Rigidbody == null)
            {
                Plugin.Logger.LogWarning($"NetworkedPhysicsObject initialized without Rigidbody on {name}");
                return;
            }

            // Ensure stable client behavior.
            // NOTE: on a listen host, we do NOT want to force kinematic, so gate by !IsServer.
            if (!FikaBackendUtils.IsServer)
                Rigidbody.isKinematic = true;

            double nowServer = NetworkTime.ServerNowSeconds;
            _spawnServerTime = nowServer;

            _lastSentPos = Rigidbody.position;
            _lastSentRot = Rigidbody.rotation;
            _lastSentVel = Rigidbody.velocity;
            _lastSentAngVel = Rigidbody.angularVelocity;
            _lastSentServerTime = 0;

            // Register
            if (!_byId.ContainsKey(NetId))
            {
                _byId[NetId] = this;
            }
            else
            {
                // Hash collision or duplicate path.
                Plugin.Logger.LogWarning($"Duplicate NetId {NetId} for '{PathId}'. Physics sync may be incorrect.");
            }

            _all.Add(this);
        }

        private void OnDestroy()
        {
            if (NetId != 0 && _byId.TryGetValue(NetId, out var cur) && cur == this)
                _byId.Remove(NetId);

            _all.Remove(this);
        }

        // --------------------- Server: dirty check ---------------------
        public bool ShouldSendServer(double serverNowSeconds)
        {
            if (Rigidbody == null)
                return false;

            bool sleeping = Rigidbody.IsSleeping();
            bool withinSpawnBurst = serverNowSeconds - _spawnServerTime <= AlwaysSendAfterSpawnSeconds;

            if (withinSpawnBurst)
                return true;

            if (!sleeping)
                return true;

            // Keepalive for sleeping objects
            if (_lastSentServerTime <= 0 || (serverNowSeconds - _lastSentServerTime) >= KeepAliveSeconds)
                return true;

            // Threshold deltas
            Vector3 pos = Rigidbody.position;
            Quaternion rot = Rigidbody.rotation;
            Vector3 vel = Rigidbody.velocity;
            Vector3 angVel = Rigidbody.angularVelocity;

            if ((pos - _lastSentPos).sqrMagnitude > (PosEpsilon * PosEpsilon))
                return true;

            if (Quaternion.Angle(rot, _lastSentRot) > RotEpsilonDeg)
                return true;

            if ((vel - _lastSentVel).sqrMagnitude > (VelEpsilon * VelEpsilon))
                return true;

            if ((angVel - _lastSentAngVel).sqrMagnitude > (AngVelEpsilon * AngVelEpsilon))
                return true;

            return false;
        }

        public void MarkSentServer(double serverNowSeconds)
        {
            if (Rigidbody == null)
                return;

            _lastSentPos = Rigidbody.position;
            _lastSentRot = Rigidbody.rotation;
            _lastSentVel = Rigidbody.velocity;
            _lastSentAngVel = Rigidbody.angularVelocity;
            _lastSentServerTime = serverNowSeconds;
        }

        public SnapshotFlags GetCurrentFlags()
        {
            if (Rigidbody == null)
                return SnapshotFlags.None;

            SnapshotFlags f = SnapshotFlags.None;
            if (Rigidbody.IsSleeping())
                f |= SnapshotFlags.Sleeping;
            return f;
        }

        // --------------------- Client: buffer + interpolation ---------------------
        public void PushSnapshot(double serverTimeSeconds, Vector3 pos, Quaternion rot, Vector3 vel, Vector3 angVel, byte flags)
        {
            var snap = new Snapshot
            {
                serverTime = serverTimeSeconds,
                position = pos,
                rotation = rot,
                velocity = vel,
                angularVelocity = angVel,
                flags = (SnapshotFlags)flags
            };

            // Latest auth is used for reconciliation during prediction.
            if (!_hasLatestAuth || snap.serverTime >= _latestAuth.serverTime)
            {
                _latestAuth = snap;
                _hasLatestAuth = true;
            }

            // Insert in ring (assume mostly in-order)
            int idx = (_snapshotHead + _snapshotCount) % SnapshotBufferSize;
            _snapshots[idx] = snap;

            if (_snapshotCount < SnapshotBufferSize)
            {
                _snapshotCount++;
            }
            else
            {
                // Overwrite oldest
                _snapshotHead = (_snapshotHead + 1) % SnapshotBufferSize;
            }

            // If we have no history yet, snap immediately so objects don't stay at origin.
            if (_snapshotCount == 1 && Rigidbody != null)
            {
                Rigidbody.position = pos;
                Rigidbody.rotation = rot;
            }
        }

        public void ClientFixedUpdate(double targetServerTime)
        {
            if (Rigidbody == null)
                return;

            if (FikaBackendUtils.IsServer)
                return;

            bool predicting = NetworkTime.LocalNowSeconds < _predictionEndLocalTime;

            if (predicting)
            {
                // During prediction we let the rigidbody simulate, but we reconcile toward latest auth.
                if (_hasLatestAuth)
                {
                    float dist = Vector3.Distance(Rigidbody.position, _latestAuth.position);
                    float ang = Quaternion.Angle(Rigidbody.rotation, _latestAuth.rotation);

                    if (dist > HardSnapDistance || ang > HardSnapAngleDeg)
                    {
                        Rigidbody.position = _latestAuth.position;
                        Rigidbody.rotation = _latestAuth.rotation;
                        Rigidbody.velocity = _latestAuth.velocity;
                        Rigidbody.angularVelocity = _latestAuth.angularVelocity;
                    }
                    else
                    {
                        // Soft correction.
                        Rigidbody.position = Vector3.Lerp(Rigidbody.position, _latestAuth.position, 0.10f);
                        Rigidbody.rotation = Quaternion.Slerp(Rigidbody.rotation, _latestAuth.rotation, 0.10f);
                    }
                }

                return;
            }

            // Not predicting: kinematic interpolation
            if (!Rigidbody.isKinematic)
            {
                Rigidbody.isKinematic = true;
                Rigidbody.velocity = Vector3.zero;
                Rigidbody.angularVelocity = Vector3.zero;
            }

            if (!TrySample(targetServerTime, out var a, out var b))
            {
                // Not enough data; if we have at least one snapshot, use the newest.
                if (_hasLatestAuth)
                {
                    Rigidbody.MovePosition(_latestAuth.position);
                    Rigidbody.MoveRotation(_latestAuth.rotation);
                }
                return;
            }

            float t = (float)((targetServerTime - a.serverTime) / Math.Max(0.0001, (b.serverTime - a.serverTime)));
            t = Mathf.Clamp01(t);

            Vector3 ipos = Vector3.Lerp(a.position, b.position, t);
            Quaternion irot = Quaternion.Slerp(a.rotation, b.rotation, t);

            Rigidbody.MovePosition(ipos);
            Rigidbody.MoveRotation(irot);
        }

        private bool TrySample(double targetServerTime, out Snapshot a, out Snapshot b)
        {
            a = default;
            b = default;

            if (_snapshotCount < 2)
                return false;

            // Walk oldest->newest looking for bracketing times.
            Snapshot prev = _snapshots[_snapshotHead];
            for (int i = 1; i < _snapshotCount; i++)
            {
                int idx = (_snapshotHead + i) % SnapshotBufferSize;
                Snapshot cur = _snapshots[idx];

                if (targetServerTime <= cur.serverTime)
                {
                    a = prev;
                    b = cur;
                    return true;
                }

                prev = cur;
            }

            // target is newer than all snapshots
            return false;
        }

        // --------------------- Local collision -> prediction window ---------------------
        private void OnCollisionEnter(Collision collision)
        {
            if (!Plugin.Active.Value)
                return;

            // Prediction only makes sense on non-authoritative clients.
            if (FikaBackendUtils.IsServer)
                return;

            if (collision == null || collision.collider == null)
                return;

            if (IsLocalPlayerCollider(collision.collider))
            {
                BeginPredictionWindow();
            }
        }

        private void OnCollisionStay(Collision collision)
        {
            // Extend window while continuously pushing
            if (!Plugin.Active.Value)
                return;

            // Prediction only makes sense on non-authoritative clients.
            if (FikaBackendUtils.IsServer)
                return;

            if (collision == null || collision.collider == null)
                return;

            if (IsLocalPlayerCollider(collision.collider))
            {
                BeginPredictionWindow();
            }
        }

        private bool IsLocalPlayerCollider(Collider col)
        {
            try
            {
                var gw = Singleton<GameWorld>.Instance;
                var mainPlayer = gw?.MainPlayer;
                if (mainPlayer == null)
                    return false;

                // EFT.Player.Transform is a BifacialTransform in EFT; use the Unity GameObject root instead.
                Transform playerRoot = mainPlayer.gameObject != null ? mainPlayer.gameObject.transform : null;
                if (playerRoot == null)
                    return false;

                return col.transform != null && col.transform.IsChildOf(playerRoot);
            }
            catch
            {
                return false;
            }
        }

        private void BeginPredictionWindow()
        {
            _predictionEndLocalTime = (float)(NetworkTime.LocalNowSeconds + PredictionWindowSeconds);

            if (Rigidbody == null)
                return;

            if (Rigidbody.isKinematic)
            {
                Rigidbody.isKinematic = false;
            }
        }

        // --------------------- Hashing ---------------------
        private static uint Hash32(string s)
        {
            if (string.IsNullOrEmpty(s))
                return 0;

            // FNV-1a 32-bit over UTF8 bytes
            const uint offset = 2166136261;
            const uint prime = 16777619;
            uint hash = offset;

            byte[] bytes = Encoding.UTF8.GetBytes(s);
            for (int i = 0; i < bytes.Length; i++)
            {
                hash ^= bytes[i];
                hash *= prime;
            }

            // Never return 0 (reserved as uninitialized)
            if (hash == 0)
                hash = 1;

            return hash;
        }
    }
}
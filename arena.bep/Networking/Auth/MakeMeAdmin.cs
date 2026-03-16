using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Fika.Core.Main.Utils;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.Core;
using ifp.arena.bep.networking.Base;
using ifp.arena.bep.networking.Base.RateLimiting;
using MemoryPack;

namespace ifp.arena.bep.networking
{
    public enum AdminAuthStep
    {
        Request,    // Client -> Server
        Challenge,  // Server -> Client
        Verify,     // Client -> Server
        Success     // Server -> Client (Confirmation)
    }

    [MemoryPackable]
    public partial struct AdminAuthPacket : INetSerializable
    {
        public AdminAuthStep Step;
        public string Payload;

        public void Serialize(NetDataWriter writer) => MemoryPackHelper.Serialize(writer, this);
        public void Deserialize(NetDataReader reader) => this = MemoryPackHelper.Deserialize<AdminAuthPacket>(reader);
    }

    // Currently runs automatically, pretty wasteful
    public class AdminLoginPacketHandler : PacketHandler<AdminAuthPacket>
    {
        private readonly Dictionary<int, string> _pendingChallenges = new();

        protected override RateLimitConfig ServerRateLimit => new(
            enabled: true,
            refillPerSecond: 0.5,
            burst: 3,
            costPerPacket: 1,
            action: RateLimitAction.Reject,
            stateTtlSeconds: 60,
            rejectCooldownSeconds: 2
        );

        protected override bool ShouldBroadcastClientPacket(AdminAuthPacket packet) => false;

        public void Send()
        {
            if (FikaBackendUtils.IsServer)
            {
                H.MainPlayerScore.IsAdmin = true;
                return;
            }

            var packet = new AdminAuthPacket
            {
                Step = AdminAuthStep.Request,
                Payload = ""
            };
            RequestSend(packet);
        }

        public override bool ServerValidation(ref AdminAuthPacket packet, NetPeer netPeer)
        {
            switch (packet.Step)
            {
                case AdminAuthStep.Request:
                    HandleLoginRequest(netPeer);
                    return true;

                case AdminAuthStep.Verify:
                    return HandleVerification(packet.Payload, netPeer);

                default:
                    return false;
            }
        }

        public override void WhenApproved(AdminAuthPacket packet, NetPeer netPeer)
        {
            if (packet.Step == AdminAuthStep.Challenge)
            {
                if (string.IsNullOrEmpty(Plugin.Password.Value)) return;

                string responseHash = ComputeHash(Plugin.Password.Value, packet.Payload);

                var responsePacket = new AdminAuthPacket
                {
                    Step = AdminAuthStep.Verify,
                    Payload = responseHash
                };

                RequestSend(responsePacket);
            }
            else if (packet.Step == AdminAuthStep.Verify)
            {
                MakePeerAdmin(netPeer);

                var successPacket = new AdminAuthPacket { Step = AdminAuthStep.Success };
                H.FikaNet.SendDataToPeer(ref successPacket, deliveryMethod, netPeer);
            }
            else if (packet.Step == AdminAuthStep.Success)
            {
                H.MainPlayerScore.IsAdmin = true;
            }
        }

        public override void WhenRejected(AdminAuthPacket packet, NetPeer netPeer)
        {
            // H.Notify("Rejected");
        }

        private void HandleLoginRequest(NetPeer peer)
        {
            string nonce = Guid.NewGuid().ToString("N");

            if (_pendingChallenges.ContainsKey(peer.Id))
                _pendingChallenges[peer.Id] = nonce;
            else
                _pendingChallenges.Add(peer.Id, nonce);

            var challengePacket = new AdminAuthPacket
            {
                Step = AdminAuthStep.Challenge,
                Payload = nonce
            };

            H.FikaNet.SendDataToPeer(ref challengePacket, deliveryMethod, peer);
        }

        private bool HandleVerification(string clientHash, NetPeer peer)
        {
            if (!_pendingChallenges.TryGetValue(peer.Id, out string nonce))
            {
                Plugin.Logger.LogWarning($"Auth failed: No challenge found for {peer.Id}");
                return false;
            }

            _pendingChallenges.Remove(peer.Id);

            string serverPassword = Plugin.Password.Value;
            if (string.IsNullOrEmpty(serverPassword)) return false;

            string expectedHash = ComputeHash(serverPassword, nonce);

            return clientHash == expectedHash;
        }

        private void MakePeerAdmin(NetPeer peer)
        {
            var player = H.GetPlayer(peer.Id);
            if (player != null)
            {
                H.GetPlayerScore(peer.Id).IsAdmin = true;
            }
        }

        private static string ComputeHash(string password, string nonce)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                string rawData = password + nonce;
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(rawData));

                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }
    }
}

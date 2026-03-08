using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Comfort.Common;
using EFT;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.Core;
using ifp.arena.bep.networking.Base;
using ifp.arena.bep.networking.Base.RateLimiting;

namespace ifp.arena.bep.networking
{
    public enum AdminAuthStep
    {
        Request,    // Client -> Server
        Challenge,  // Server -> Client
        Verify      // Client -> Server
    }

    public struct AdminAuthPacket : INetSerializable
    {
        public AdminAuthStep Step;
        public string Payload;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put((int)Step);
            writer.Put(Payload ?? string.Empty);
        }

        public void Deserialize(NetDataReader reader)
        {
            Step = (AdminAuthStep)reader.GetInt();
            Payload = reader.GetString();
        }
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
                    return false; // Return false to stop "WhenApproved" from firing yet

                case AdminAuthStep.Verify:
                    return HandleVerification(packet.Payload, netPeer);

                default:
                    return false;
            }
        }

        public override void WhenApproved(AdminAuthPacket packet, NetPeer netPeer)
        {
            // CASE 1: Client Logic (Received Challenge from Server)
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
            }
        }

        public override void WhenRejected(AdminAuthPacket packet, NetPeer netPeer)
        {
            H.Notify("Rejected");
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

            // Send only to the specific peer (Server -> Client)
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
                H.GetPlayerScore(peer.Id).isAdmin = true;
                H.Notify($"{player.name} logged in as Admin.");
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
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using EFT;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using PacketHandler;
using PacketHandler.RateLimiting;
using MemoryPack;

namespace ifp.arena.bep.networking;

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
    [MemoryPackAllowSerialize]
    public Player player;

    public AdminAuthStep Step;
    public string Payload;

    public void Serialize(NetDataWriter writer) => MemoryPackWrapper.Serialize(writer, this);
    public void Deserialize(NetDataReader reader) => this = MemoryPackWrapper.Deserialize<AdminAuthPacket>(reader);
}

// I can't tell if I should refactor this thing to be more inline; though who cares if it's slightly different so long as it works I guess?
public class AdminLoginPacketHandler : PacketHandler<AdminAuthPacket>
{
    private readonly Dictionary<Player, string> _pendingChallenges = new();

    protected override RateLimitConfig ServerRateLimit => new(
        enabled: true,
        refillPerSecond: 0.5,
        burst: 3,
        costPerPacket: 1,
        action: RateLimitAction.Reject,
        stateTtlSeconds: 60,
        rejectCooldownSeconds: 2
    );

    protected override bool ShouldBroadcastPacket(AdminAuthPacket packet) => false;

    public void Send()
    {
        if (H.IsServer)
        {
            H.MainPlayerScore.isAdmin = true;
            return;
        }

        var packet = new AdminAuthPacket
        {
            player = H.MainPlayer,
            Step = AdminAuthStep.Request,
            Payload = ""
        };

        DispatchPacket(packet);
    }

    protected override bool PacketValidation(ref AdminAuthPacket packet, NetPeer peer)
    {
        switch (packet.Step)
        {
            case AdminAuthStep.Request:
                HandleLoginRequest(packet, peer);
                return true;

            case AdminAuthStep.Verify:
                return HandleVerification(packet);

            default:
                D.Log($"AdminLoginPacketHandler [Server]: Unhandled Step {packet.Step} in ServerValidation. Rejecting.");
                return false;
        }
    }

    protected override void WhenApproved(AdminAuthPacket packet, NetPeer peer)
    {
        if (packet.Step == AdminAuthStep.Challenge)
        {
            if (string.IsNullOrEmpty(Plugin.Password.Value))
            {
                D.Log("AdminLoginPacketHandler [Client]: Local Plugin.Password.Value is empty! Aborting verification.");
                return;
            }

            string responseHash = ComputeHash(Plugin.Password.Value, packet.Payload);

            var responsePacket = new AdminAuthPacket
            {
                player = H.MainPlayer,
                Step = AdminAuthStep.Verify,
                Payload = responseHash
            };

            DispatchPacket(responsePacket);
        }
        else if (packet.Step == AdminAuthStep.Verify)
        {
            var successPacket = new AdminAuthPacket
            {
                player = packet.player,
                Step = AdminAuthStep.Success
            };

            H.FikaNet.SendDataToPeer(ref successPacket, deliveryMethod, peer);
        }
        else if (packet.Step == AdminAuthStep.Success)
        {
            D.Log($"{packet.player.Profile.Nickname} is now an Admin");
            H.GetPlayerScore(packet.player)?.isAdmin = true;
        }
    }

    protected override void WhenRejected(AdminAuthPacket packet, NetPeer peer)
    {
        D.Notify("Rejected");
    }

    private void HandleLoginRequest(AdminAuthPacket packet, NetPeer peer)
    {
        string nonce = Guid.NewGuid().ToString("N");

        _pendingChallenges[packet.player] = nonce;

        var challengePacket = new AdminAuthPacket
        {
            player = packet.player,
            Step = AdminAuthStep.Challenge,
            Payload = nonce
        };

        D.Log(packet.player.Profile.Nickname);

        H.FikaNet.SendDataToPeer(ref challengePacket, deliveryMethod, peer);
    }

    private bool HandleVerification(AdminAuthPacket packet)
    {
        if (!_pendingChallenges.TryGetValue(packet.player, out string nonce))
        {
            D.Log($"Auth failed: No challenge found for {packet.player.Profile.Nickname}");
            return false;
        }

        _pendingChallenges.Remove(packet.player);

        string serverPassword = Plugin.Password.Value;
        if (string.IsNullOrEmpty(serverPassword))
        {
            D.Log("AdminLoginPacketHandler [Server]: Verification failed. Server password (Plugin.Password.Value) is empty/null!");
            return false;
        }

        string expectedHash = ComputeHash(serverPassword, nonce);
        bool isMatch = (packet.Payload == expectedHash);

        return isMatch;
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
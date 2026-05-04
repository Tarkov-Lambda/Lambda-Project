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

public class AdminLoginPacketHandler : PacketHandler<AdminAuthPacket>
{
    private readonly Dictionary<Player, string> _pendingChallenges = new();

    protected override RateLimitConfig ServerRateLimit => RateLimitPresets.LimitPerSecond(5);

    protected override bool ShouldNotifyAboutRejection => true;

    public void Send()
    {
        if (H.IsServer)
        {
            H.MainPlayerScore.SetAdmin(true);
            return;
        }

        var packet = new AdminAuthPacket
        {
            player = H.MainPlayer,
            Step = AdminAuthStep.Request,
            Payload = null
        };

        DispatchPacket(packet);
    }

    protected override bool ValidatePacket(AdminAuthPacket packet, NetPeer peer, out string rejectionReason)
    {
        rejectionReason = null;

        switch (packet.Step)
        {
            case AdminAuthStep.Request:
                return true;

            case AdminAuthStep.Verify:
                return ValidateVerification(packet, out rejectionReason);

            default:
                rejectionReason = $"Unhandled auth step {packet.Step}";
                return false;
        }
    }

    private bool ValidateVerification(AdminAuthPacket packet, out string rejectionReason)
    {
        rejectionReason = null;

        if (!_pendingChallenges.TryGetValue(packet.player, out var nonce))
        {
            rejectionReason = "No pending challenge.";
            return false;
        }

        string serverPassword = Plugin.Password.Value;
        if (string.IsNullOrEmpty(serverPassword))
        {
            rejectionReason = "Server password not configured.";
            return false;
        }

        string expected = ComputeHash(serverPassword, nonce);
        bool valid = packet.Payload == expected;

        if (!valid)
            rejectionReason = "Invalid credentials.";

        return valid;
    }

    protected override void ProcessApprovedPacket(ref AdminAuthPacket packet, NetPeer peer)
    {
        MutateApprovedPacket(ref packet, peer);

        switch (packet.Step)
        {
            case AdminAuthStep.Request:
                HandleRequest(ref packet, peer);
                break;

            case AdminAuthStep.Verify:
                HandleVerify(ref packet, peer);
                break;
        }

        ApplyInternal(packet, peer);
    }

    private void HandleRequest(ref AdminAuthPacket packet, NetPeer peer)
    {
        string nonce = Guid.NewGuid().ToString("N");

        _pendingChallenges[packet.player] = nonce;

        var challenge = new AdminAuthPacket
        {
            player = packet.player,
            Step = AdminAuthStep.Challenge,
            Payload = nonce
        };

        H.FikaNet.SendDataToPeer(ref challenge, DeliveryMethod, peer);
    }

    private void HandleVerify(ref AdminAuthPacket packet, NetPeer peer)
    {
        _pendingChallenges.Remove(packet.player);

        var success = new AdminAuthPacket
        {
            player = packet.player,
            Step = AdminAuthStep.Success,
            Payload = null
        };

        H.FikaNet.SendDataToPeer(ref success, DeliveryMethod, peer);
    }


    protected override void Apply(AdminAuthPacket packet, NetPeer peer)
    {
        switch (packet.Step)
        {
            case AdminAuthStep.Challenge:
                HandleChallenge(packet);
                break;

            case AdminAuthStep.Success:
                HandleSuccess(packet);
                break;
        }
    }

    private void HandleChallenge(AdminAuthPacket packet)
    {
        if (string.IsNullOrEmpty(Plugin.Password.Value))
        {
            D.Log("Admin auth failed: client password is empty.");
            return;
        }

        string hash = ComputeHash(Plugin.Password.Value, packet.Payload);

        var verify = new AdminAuthPacket
        {
            player = H.MainPlayer,
            Step = AdminAuthStep.Verify,
            Payload = hash
        };

        DispatchPacket(verify);
    }

    private void HandleSuccess(AdminAuthPacket packet)
    {
        D.Log($"{packet.player.Profile.Nickname} is now an Admin");
        H.GetPlayerScore(packet.player)?.SetAdmin(true);
    }

    private static string ComputeHash(string password, string nonce)
    {
        using var sha256 = SHA256.Create();

        byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password + nonce));

        var builder = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes)
            builder.Append(b.ToString("x2"));

        return builder.ToString();
    }
}
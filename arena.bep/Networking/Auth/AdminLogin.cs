using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using EFT;
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
public partial struct AdminAuthPacket : IPacket
{
    [MemoryPackAllowSerialize]
    public Player player;

    public AdminAuthStep Step;
    public string Payload;
}

public class AdminLoginPacketHandler : LambdaPacketHandler<AdminAuthPacket>
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

    protected override bool ValidatePacket(AdminAuthPacket packet, int peerId, out string rejectionReason)
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

    protected override void ProcessApprovedPacket(ref AdminAuthPacket packet, int peerId)
    {
        MutateApprovedPacket(ref packet, peerId);

        switch (packet.Step)
        {
            case AdminAuthStep.Request:
                HandleRequest(ref packet, peerId);
                break;

            case AdminAuthStep.Verify:
                HandleVerify(ref packet, peerId);
                break;
        }

        ApplyInternal(packet, peerId);
    }

    private void HandleRequest(ref AdminAuthPacket packet, int peerId)
    {
        string nonce = Guid.NewGuid().ToString("N");

        _pendingChallenges[packet.player] = nonce;

        var challenge = new AdminAuthPacket
        {
            player = packet.player,
            Step = AdminAuthStep.Challenge,
            Payload = nonce
        };

        PacketHandlerUtils.Network.SendDataToPeer(ref challenge, DeliveryType, peerId);
    }

    private void HandleVerify(ref AdminAuthPacket packet, int peerId)
    {
        _pendingChallenges.Remove(packet.player);

        var success = new AdminAuthPacket
        {
            player = packet.player,
            Step = AdminAuthStep.Success,
            Payload = null
        };

        PacketHandlerUtils.Network.SendDataToPeer(ref success, DeliveryType, peerId);
    }


    protected override void Apply(AdminAuthPacket packet, int peerId)
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
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using EFT;
using PacketWarden.RateLimiting;
using MemoryPack;
using Comfort.Common;

namespace Lambda.Core.Networking;

public enum AdminAuthStep
{
    Request,    // Client -> Server
    Challenge,  // Server -> Client
    Verify,     // Client -> Server
    Success     // Server -> Client (Confirmation)
}

[MemoryPackable]
public partial struct AdminAuthPacket : IPacket, IAuthoredPacket
{
    [MemoryPackAllowSerialize]
    public Player Player { get; set; }

    public AdminAuthStep Step;
    public string Payload;
}

public class AdminLoginPacketWarden : LambdaPacketWarden<AdminAuthPacket>
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
            Player = H.MainPlayer,
            Step = AdminAuthStep.Request,
            Payload = null
        };

        DispatchPacket(ref packet);
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

        if (!_pendingChallenges.TryGetValue(packet.Player, out var nonce))
        {
            rejectionReason = "No pending challenge.";
            return false;
        }

        string serverPassword = LambdaPlugin.Password.Value;
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

        _pendingChallenges[packet.Player] = nonce;

        var challenge = new AdminAuthPacket
        {
            Player = packet.Player,
            Step = AdminAuthStep.Challenge,
            Payload = nonce
        };

        DispatchPacket(ref challenge, peerId);
    }

    private void HandleVerify(ref AdminAuthPacket packet, int peerId)
    {
        _pendingChallenges.Remove(packet.Player);

        var success = new AdminAuthPacket
        {
            Player = packet.Player,
            Step = AdminAuthStep.Success,
            Payload = null
        };

        DispatchPacket(ref success, peerId);
        ApplyInternal(success, INetworkBackend.LocalPeerId);
    }


    protected override void Apply(AdminAuthPacket packet, int peerId)
    {
        switch (packet.Step)
        {
            case AdminAuthStep.Challenge:
                HandleChallenge(packet, peerId);
                break;

            case AdminAuthStep.Success:
                HandleSuccess(packet, peerId);
                break;
        }
    }

    private void HandleChallenge(AdminAuthPacket packet, int peerId)
    {
        if (string.IsNullOrEmpty(LambdaPlugin.Password.Value))
        {
            D.Log("Admin auth failed: client password is empty.");
            return;
        }

        string hash = ComputeHash(LambdaPlugin.Password.Value, packet.Payload);

        var verify = new AdminAuthPacket
        {
            Player = H.MainPlayer,
            Step = AdminAuthStep.Verify,
            Payload = hash
        };

        DispatchPacket(ref verify);
    }

    private void HandleSuccess(AdminAuthPacket packet, int peerId)
    {
        packet.Player.GetContext()?.SetAdmin(true);

        if (H.IsServer)
        {
            Singleton<ServerMessagePacketWarden>.Instance.SendToPeer($"Your priviledges have been elevated.", PacketWardenUtils.Network.GetPeerIdByPlayer(packet.Player));
        }
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
using System.Linq;
using EFT;
using MemoryPack;
using PacketWarden.RateLimiting;

namespace Lambda.Core.Networking;

[MemoryPackable]
public partial struct ClanTagResyncPacket : IPacket, IAuthoredPacket
{
    [MemoryPackAllowSerialize]
    public Player Player { get; set; }

    public string newClanTag;
}

public class ClanTagResyncPacketWarden : LambdaPacketWarden<ClanTagResyncPacket>
{
    protected override RateLimitConfig ServerRateLimit => RateLimitPresets.LimitByCooldown(5);
    protected override bool ShouldNotifyAboutRejection => true;
    protected override bool ShouldDisplayRejectionInChat => true;

    public void Send(string newClanTag)
    {
        var packet = new ClanTagResyncPacket
        {
            Player = H.MainPlayer,
            newClanTag = newClanTag
        };

        DispatchPacket(ref packet);
    }

    protected override bool ValidatePacket(ClanTagResyncPacket packet, int peerId, out string rejectionReason)
    {
        packet.newClanTag = packet.newClanTag?.Trim();

        if (packet.newClanTag.IsNullOrEmpty())
        {
            rejectionReason = "Clan Tag cannot be empty";
            return false;
        }

        if (packet.newClanTag.Length > 3)
        {
            rejectionReason = "Clan Tag cannot exceed 3 characters";
            return false;
        }

        for (int i = 0; i < packet.newClanTag.Length; i++)
        {
            if (!char.IsLetter(packet.newClanTag[i]))
            {
                rejectionReason = "Clan Tag can only contain letters";
                return false;
            }
        }

        return base.ValidatePacket(packet, peerId, out rejectionReason);
    }

    protected override void MutateApprovedPacket(ref ClanTagResyncPacket packet, int peerId)
    {
        packet.newClanTag = packet.newClanTag.ToUpper();
        base.MutateApprovedPacket(ref packet, peerId);
    }

    protected override void Apply(ClanTagResyncPacket packet, int peerId)
    {
        if (packet.Player.IsYourPlayer)
        {
            LambdaPlugin.ClanTag.Value = packet.newClanTag;
        }
        packet.Player.Context.SetClanTag(packet.newClanTag);
    }
}
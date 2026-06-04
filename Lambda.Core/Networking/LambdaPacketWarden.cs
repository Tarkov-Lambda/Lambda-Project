using Comfort.Common;
using Lambda.Core.Networking;
using PacketWarden;

public abstract class LambdaPacketWarden<T> : PacketWarden<T> where T : IPacket, new()
{
    // Make sure arena is initialized before we apply this packet type
    // This is here to essentially ignore all packets until our client player is actually ready to receive them (ie the scoreboard is initialized)
    // later on it might be better to hook the arena initialization into loading process
    // however for now it works and should not be touched
    protected virtual bool ShouldApplyBeforeArenaInitialized => false;

    protected virtual bool ShouldDisplayRejectionInChat => false;

    public bool IsArenaReady => H.Arena?.Session != null;

    protected override void WhenClientReceivesPacket(T packet, int peerId)
    {
        if (!ShouldApplyBeforeArenaInitialized && !IsArenaReady) return;

        base.WhenClientReceivesPacket(packet, peerId);
    }

    protected override bool IsUnauthorized(int id)
    {
        if (H.IsServer) return false;

        if (Authority == PacketAuthority.Admin)
        {
            PlayerContext score = H.GetPlayerContext(id);
            return score == null || !score.IsAdmin; // unauthorized only if NOT admin
        }
        else if (Authority == PacketAuthority.ServerOnly && id != H.MainPlayer.Id)
        {
            return true;
        }

        return false;
    }

    protected override void Notify(string rejectionReason)
    {
        if (ShouldDisplayRejectionInChat)
        {
            ServerMessagePacket RejectionReasonChatWrapperPacket = new()
            {
                msg = rejectionReason
            };
            // not the cleanest pattern, but at least we pass it through the Packet Warden in a sense?
            Singleton<GenericMessagePacketWarden>.Instance.OnRejectionMessageInChat?.Invoke(RejectionReasonChatWrapperPacket);
        }
        else
        {
            base.Notify(rejectionReason);
        }
    }
}
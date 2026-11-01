using Comfort.Common;
using EFT;
using Lambda.Core.Networking;
using PacketWarden;

public abstract class LambdaPacketWarden<T> : PacketWarden<T>
    where T : IPacket, new()
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
        if (!ShouldApplyBeforeArenaInitialized && !IsArenaReady)
            return;

        base.WhenClientReceivesPacket(packet, peerId);
    }

    protected override bool IsAdmin(int peerId)
    {
        if (base.IsAdmin(peerId))
            return true;

        Player player = Network.GetPlayerByPeerId(peerId);
        return player != null && player.Context?.IsAdmin == true;
    }

    protected override void Notify(string rejectionReason)
    {
        if (ShouldDisplayRejectionInChat)
        {
            ServerMessagePacket RejectionReasonChatWrapperPacket = new() { msg = rejectionReason };
            // not the cleanest pattern, but at least we pass it through the Packet Warden in a sense?
            Singleton<GenericMessagePacketWarden>.Instance.OnRejectionMessageInChat?.Invoke(
                RejectionReasonChatWrapperPacket
            );
        }
        else
        {
            base.Notify(rejectionReason);
        }
    }
}

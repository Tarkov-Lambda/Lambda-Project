

using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using PacketHandler;

public abstract class LambdaPacketHandler<T> : PacketHandler<T> where T : INetSerializable, new()
{
    // Make sure arena is initialized before we apply this packet type
    // This is here to essentially ignore all packets until our client player is actually ready to receive them (ie the scoreboard is initialized)
    // later on it might be better to hook the arena initialization into loading process
    // however for now it works and should not be touched
    protected virtual bool ShouldApplyBeforeArenaInitialized => false;

    protected override void WhenClientReceivesPacket(T packet, NetPeer peer)
    {
        if (!ShouldApplyBeforeArenaInitialized && H.Arena?.Session == null) return;

        base.WhenClientReceivesPacket(packet, peer);
    }

    protected override bool IsUnauthorized(int id)
    {
        if (H.IsServer) return false;
        
        if (Authority == PacketAuthority.Admin)
        {
            PlayerScore score = H.GetPlayerScore(id);
            return score == null || !score.IsAdmin; // unauthorized only if NOT admin
        }
        else if (Authority == PacketAuthority.ServerOnly && id != H.MainPlayer.Id)
        {
            return true;
        }

        return false;
    }
}
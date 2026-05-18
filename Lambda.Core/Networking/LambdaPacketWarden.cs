using PacketWarden;

public abstract class LambdaPacketWarden<T> : PacketWarden<T> where T : IPacket, new()
{
    // Make sure arena is initialized before we apply this packet type
    // This is here to essentially ignore all packets until our client player is actually ready to receive them (ie the scoreboard is initialized)
    // later on it might be better to hook the arena initialization into loading process
    // however for now it works and should not be touched
    protected virtual bool ShouldApplyBeforeArenaInitialized => false;

    protected override void WhenClientReceivesPacket(T packet, int peerId)
    {
        if (!ShouldApplyBeforeArenaInitialized && H.Arena?.Session == null) return;

        base.WhenClientReceivesPacket(packet, peerId);
    }

    protected override bool IsUnauthorized(int id)
    {
        if (H.IsServer) return false;
        
        if (Authority == PacketAuthority.Admin)
        {
            PlayerContext score = H.GetPlayerScore(id);
            return score == null || !score.IsAdmin; // unauthorized only if NOT admin
        }
        else if (Authority == PacketAuthority.ServerOnly && id != H.MainPlayer.Id)
        {
            return true;
        }

        return false;
    }
}
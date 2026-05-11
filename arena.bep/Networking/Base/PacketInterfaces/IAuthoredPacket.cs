using EFT;

namespace ifp.arena.bep.networking;

/// <summary>
/// Peer can send only for its own player, and server can send for any player
/// </summary>
public interface IAuthoredPacket
{
    public Player Player { get; set; }
}
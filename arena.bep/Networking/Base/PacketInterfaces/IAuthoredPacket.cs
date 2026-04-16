using EFT;

namespace ifp.arena.bep.networking;

/// <summary>
/// Server validated packet that will be 
/// </summary>
public interface IAuthoredPacket
{
    /// <summary>
    /// 
    /// </summary>
    public Player Player { get; set; }
}
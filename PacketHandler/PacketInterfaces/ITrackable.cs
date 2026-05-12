using System;

namespace ifp.arena.bep.networking;

/// <summary>
/// Packet can be tracked
/// </summary>
public interface ITrackable
{
    public Guid ID { get; set; }
}
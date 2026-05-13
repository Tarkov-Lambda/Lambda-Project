using System;

/// <summary>
/// Packet can be tracked
/// </summary>
public interface ITrackablePacket
{
    public Guid ID { get; set; }
}
/// <summary>
/// Server ultimately decides the timestamp of the packet. Must be manually overidden in MutateApprovedPacket
/// </summary>
public interface IServerTimestampedPacket
{
    public double Timestamp { get; set; }
}
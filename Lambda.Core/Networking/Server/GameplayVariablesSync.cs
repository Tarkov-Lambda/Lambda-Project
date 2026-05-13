using PacketWarden;
using MemoryPack;

namespace Lambda.Core.Networking;

[MemoryPackable]
public partial struct GameplayVariablesSyncPacket : IPacket
{
    public GameplayVariablesStruct variables;
}

public class GameplayVariablesSyncPacketWarden : LambdaPacketWarden<GameplayVariablesSyncPacket>
{
    protected override PacketAuthority Authority => PacketAuthority.Admin;

    public void Send()
    {
        var packet = new GameplayVariablesSyncPacket
        {
            variables = GameplayVariables.vars
        };

        DispatchPacket(packet);
    }

    public void SendToPeer(int peerId)
    {
        var packet = new GameplayVariablesSyncPacket
        {
            variables = GameplayVariables.vars
        };
        
        DispatchPacket(packet, peerId);
    }

    protected override void Apply(GameplayVariablesSyncPacket packet, int peerId)
    {
        GameplayVariables.vars = packet.variables;
    }
}
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using PacketHandler;
using MemoryPack;
using ifp.arena.bep.Core.AssetBundleHandling;

namespace ifp.arena.bep.networking;

[MemoryPackable]
public partial struct GameplayVariablesSyncPacket : IPacket
{
    public GameplayVariablesStruct variables;
}

public class GameplayVariablesSyncPacketHandler : LambdaPacketHandler<GameplayVariablesSyncPacket>
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
        
        DispatchPacketToPeer(packet, peerId);
    }

    protected override void Apply(GameplayVariablesSyncPacket packet, int peerId)
    {
        GameplayVariables.vars = packet.variables;
    }
}
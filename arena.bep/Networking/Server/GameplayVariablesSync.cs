using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using PacketHandler;
using MemoryPack;
using ifp.arena.bep.Core.AssetBundleHandling;

namespace ifp.arena.bep.networking;

[MemoryPackable]
public partial struct GameplayVariablesSyncPacket : INetSerializable
{
    public GameplayVariablesStruct variables;

    public void Serialize(NetDataWriter writer) => MemoryPackWrapper.Serialize(writer, this);
    public void Deserialize(NetDataReader reader) => this = MemoryPackWrapper.Deserialize<GameplayVariablesSyncPacket>(reader);
}

public class GameplayVariablesSyncPacketHandler : PacketHandler<GameplayVariablesSyncPacket>
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

    public void SendToPeer(NetPeer peer)
    {
        var packet = new GameplayVariablesSyncPacket
        {
            variables = GameplayVariables.vars
        };
        
        DispatchPacketToPeer(packet, peer);
    }

    protected override void Apply(GameplayVariablesSyncPacket packet, NetPeer peer)
    {
        GameplayVariables.vars = packet.variables;
    }
}
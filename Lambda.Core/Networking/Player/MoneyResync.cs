using EFT;
using Lambda.Shared;
using MemoryPack;
using PacketWarden;
using UnityEngine;

namespace Lambda.Core.Networking;

[MemoryPackable]
public partial struct MoneyResyncPacket : IPacket, IAuthoredPacket
{
    [MemoryPackAllowSerialize]
    public Player Player { get; set; }

    public int newMoney;
}

public class MoneyResyncPacketWarden : LambdaPacketWarden<MoneyResyncPacket>
{
    protected override PacketAuthority Authority => PacketAuthority.ServerOnly;

    public void Send(Player player,int newMoney)
    {
        var packet = new MoneyResyncPacket
        {
            Player = H.MainPlayer,
            newMoney = newMoney
        };

        DispatchPacket(ref packet);
    }


    protected override void Apply(MoneyResyncPacket packet, int peerId)
    {
        packet.Player.GetContext().SetMoney(packet.newMoney);
    }
}


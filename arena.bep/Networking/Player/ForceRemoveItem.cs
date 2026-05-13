using Cysharp.Threading.Tasks;
using EFT;
using EFT.InventoryLogic;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.Core;
using System;
using Fika.Core.Main.Players;
using MemoryPack;

namespace ifp.arena.bep.networking;

[MemoryPackable]
public partial struct PopPacket : IPacket, IAuthoredPacket
{
    [MemoryPackAllowSerialize]
    public Player Player { get; set; }

    [MemoryPackAllowSerialize]
    public Item item;

    [MemoryPackAllowSerialize]
    public ItemAddress itemAddress;
}

public class ForceRemoveItemPacketHandler : LambdaPacketHandler<PopPacket>
{
    // protected override RateLimitConfig ServerRateLimit => RateLimitPresets.StrictInteraction;

    public void Send(Item item)
    {
        var packet = new PopPacket
        {
            Player = H.MainPlayer,
            item = item,
            itemAddress = item.CurrentAddress,
        };

        DispatchPacket(packet);
    }

    protected override async void Apply(PopPacket packet, int peerId)
    {
        try
        {
            // sussy
            if (!packet.Player.IsYourPlayer && packet.Player is ObservedPlayer obsPlayer)
            {
                obsPlayer.OperationCallbacks.Clear();
            }

            packet.Player.TryPopItemWithoutRestriction(packet.item, packet.itemAddress);
        }
        catch (Exception e)
        {
            D.Log($"An error has occured in {GetType().Name}");
            D.Log(e.Message);
            D.Log(e.StackTrace);
        }
    }
}
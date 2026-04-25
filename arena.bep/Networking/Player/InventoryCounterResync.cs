using Comfort.Common;
using Cysharp.Threading.Tasks;
using EFT;
using EFT.InventoryLogic;
using Fika.Core.Main.ObservedClasses;
using Fika.Core.Main.Players;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using HarmonyLib;
using ifp.arena.bep.Core;
using PacketHandler;
using System;
using System.Reflection;
using System.Threading;

namespace ifp.arena.bep.networking;

public struct InventoryCounterResyncPacket : INetSerializable, IAuthoredPacket
{
    public Player Player { get; set; }
    public MongoID MongoId;

    public void Serialize(NetDataWriter writer)
    {
        writer.PutPlayer(Player);
        writer.PutMongoID(MongoId);
    }

    public void Deserialize(NetDataReader reader)
    {
        Player = reader.GetPlayer();
        MongoId = reader.GetMongoID();
    }
}

public class InventoryCounterResyncPacketHandler : PacketHandler<InventoryCounterResyncPacket>
{
    private static readonly MethodInfo _setNewIdMethod = AccessTools.Method(typeof(ObservedInventoryController), "SetNewID");

    private CancellationTokenSource _cts;

    public async UniTaskVoid Send()
    {
        _cts?.Cancel();
        _cts?.Dispose();

        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        try
        {
            await UniTask.Delay(300, cancellationToken: token);

            var packet = new InventoryCounterResyncPacket
            {
                Player = H.MainPlayer,
                MongoId = H.MainPlayer.InventoryController.CurrentId
            };

            DispatchPacket(packet);
        }
        catch (OperationCanceledException) { }
    }

    protected override void Apply(InventoryCounterResyncPacket packet, NetPeer peer)
    {
        if (packet.Player.IsYourPlayer) return;

        if (packet.Player is ObservedPlayer observedPlayer && observedPlayer.InventoryController is ObservedInventoryController observedController)
        {
            _setNewIdMethod?.Invoke(observedController, new object[] { packet.MongoId });
        }
    }
}

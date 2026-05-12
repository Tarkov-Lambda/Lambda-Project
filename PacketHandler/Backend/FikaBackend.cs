using System;
using Fika.Core.Main.Utils;
using Fika.Core.Modding.Events;
using Fika.Core.Networking.LiteNetLib;
using static Fika.Core.Modding.FikaEventDispatcher;

public class FikaBackend : INetworkBackend, IDisposable
{
    public bool IsServer => FikaBackendUtils.IsServer;
    public bool IsClient => FikaBackendUtils.IsClient;
    public bool IsHeadless => FikaBackendUtils.IsHeadless;

    public FikaBackend() => OnFikaEvent += ManageFikaEvent;
    public virtual void Dispose() => OnFikaEvent -= ManageFikaEvent;

    protected void ManageFikaEvent(FikaEvent fikaEvent)
    {
        if (fikaEvent is FikaNetworkManagerCreatedEvent) PacketHandlerUtils.TriggerNetworkCreated();
        if (fikaEvent is FikaNetworkManagerDestroyedEvent) PacketHandlerUtils.TriggerNetworkDestroyed();
    }

    public void RegisterPacketHandler<T>(Action<T, int> onReceive) where T : IPacket
    {
        H.FikaNet.RegisterPacket<FikaPacketWrapper<T>, NetPeer>(
            (wrapper, peer) => onReceive(wrapper.Payload, peer.Id)
        );
    }

    public void UnregisterPacketHandler<T>() where T : IPacket
    {
        H.FikaNet.UnregisterPacket<FikaPacketWrapper<T>>();
    }

    public void DisconnectPeer(int peerId) { }


    public void SendData<T>(ref T packet, DeliveryType method, bool broadcast) where T : IPacket
    {
        var wrapper = new FikaPacketWrapper<T> { Payload = packet };
        H.FikaNet.SendData(ref wrapper, ToDeliveryType(method), broadcast);
    }

    public void SendDataToPeer<T>(ref T packet, DeliveryType method, int id) where T : IPacket
    {
        var wrapper = new FikaPacketWrapper<T> { Payload = packet };
        H.FikaNet.SendDataToPeer(ref wrapper, ToDeliveryType(method), H.FikaNet.GetPeerById(id));
    }

    public DeliveryMethod ToDeliveryType(DeliveryType type)
    {
        return (DeliveryMethod)(byte)type;
    }
}
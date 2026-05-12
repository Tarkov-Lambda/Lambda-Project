using System;
using Comfort.Common;
using EFT;
using Fika.Core.Main.Players;
using Fika.Core.Main.Utils;
using Fika.Core.Modding.Events;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib;
using static Fika.Core.Modding.FikaEventDispatcher;

public class FikaBackend : INetworkBackend, IDisposable
{
    public bool IsServer => FikaBackendUtils.IsServer;
    public bool IsClient => FikaBackendUtils.IsClient;
    public bool IsHeadless => FikaBackendUtils.IsHeadless;
    public int NetId => Singleton<IFikaNetworkManager>.Instance.NetId;

    private readonly object OnTriggerNetworkCreated;
    private readonly object OnTriggerNetworkDestroyed;

    public FikaBackend()
    {
        OnTriggerNetworkCreated = new Action<FikaNetworkManagerCreatedEvent>(TriggerNetworkCreated);
        SubscribeEvent((Action<FikaNetworkManagerCreatedEvent>)OnTriggerNetworkCreated);

        OnTriggerNetworkDestroyed = new Action<FikaNetworkManagerDestroyedEvent>(TriggerNetworkDestroyed);
        SubscribeEvent((Action<FikaNetworkManagerDestroyedEvent>)OnTriggerNetworkDestroyed);
    }

    public virtual void Dispose()
    {
        UnsubscribeEvent((Action<FikaNetworkManagerCreatedEvent>)OnTriggerNetworkCreated);
        UnsubscribeEvent((Action<FikaNetworkManagerDestroyedEvent>)OnTriggerNetworkDestroyed);
    }

    void TriggerNetworkCreated(FikaNetworkManagerCreatedEvent ev) => PacketHandlerUtils.TriggerNetworkCreated();
    void TriggerNetworkDestroyed(FikaNetworkManagerDestroyedEvent ev) => PacketHandlerUtils.TriggerNetworkDestroyed();

    public void RegisterPacketHandler<T>(Action<T, int> onReceive) where T : IPacket
    {
        Singleton<IFikaNetworkManager>.Instance.RegisterPacket<FikaPacketWrapper<T>, NetPeer>((wrapper, peer) => onReceive(wrapper.Payload, peer.Id));
    }

    public void UnregisterPacketHandler<T>() where T : IPacket
    {
        Singleton<IFikaNetworkManager>.Instance.UnregisterPacket<FikaPacketWrapper<T>>();
    }

    public void DisconnectPeer(int peerId) { }

    public void SendData<T>(ref T packet, DeliveryType method, bool broadcast) where T : IPacket
    {
        FikaPacketWrapper<T> wrapper = new()
        {
            Payload = packet
        };

        Singleton<IFikaNetworkManager>.Instance.SendData(ref wrapper, ToDeliveryType(method), broadcast);
    }

    public void SendDataToPeer<T>(ref T packet, DeliveryType method, int id) where T : IPacket
    {
        FikaPacketWrapper<T> wrapper = new()
        {
            Payload = packet
        };

        Singleton<IFikaNetworkManager>.Instance.SendDataToPeer(ref wrapper, ToDeliveryType(method), Singleton<IFikaNetworkManager>.Instance.GetPeerById(id));
    }

    public DeliveryMethod ToDeliveryType(DeliveryType type) => (DeliveryMethod)type;

    public Player GetPlayerByPeerId(int peerId) => Singleton<IFikaNetworkManager>.Instance.GetPeerById(peerId).Player;

    public int GetPeerIdByPlayer(Player player) => (player as FikaPlayer).NetId;
}
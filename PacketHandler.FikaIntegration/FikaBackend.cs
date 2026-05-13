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
    public bool IsServer   => FikaBackendUtils.IsServer;
    public bool IsClient   => FikaBackendUtils.IsClient;
    public bool IsHeadless => FikaBackendUtils.IsHeadless;
    public bool IsOnline   => true;

    public int NetId       => Singleton<IFikaNetworkManager>.Instance.NetId;

    private readonly Action<FikaNetworkManagerCreatedEvent> OnNetworkCreated;
    private readonly Action<FikaNetworkManagerDestroyedEvent> OnNetworkDestroyed;

    public FikaBackend()
    {
        OnNetworkCreated = new Action<FikaNetworkManagerCreatedEvent>(TriggerNetworkCreated);
        SubscribeEvent(OnNetworkCreated);

        OnNetworkDestroyed = new Action<FikaNetworkManagerDestroyedEvent>(TriggerNetworkDestroyed);
        SubscribeEvent(OnNetworkDestroyed);
    }

    public void Dispose()
    {
        UnsubscribeEvent(OnNetworkCreated);
        UnsubscribeEvent(OnNetworkDestroyed);
    }

    void TriggerNetworkCreated(FikaNetworkManagerCreatedEvent ev) => PacketHandlerUtils.TriggerNetworkCreated();
    void TriggerNetworkDestroyed(FikaNetworkManagerDestroyedEvent ev) => PacketHandlerUtils.TriggerNetworkDestroyed();

    public void RegisterPacketHandler<T>(Action<T, int> onReceive) where T : IPacket
    {
        Singleton<IFikaNetworkManager>.Instance.RegisterPacket<LiteNetLibWrapper<T>, NetPeer>((wrapper, peer) => onReceive(wrapper.Payload, peer.Id));
    }

    public void UnregisterPacketHandler<T>() where T : IPacket
    {
        Singleton<IFikaNetworkManager>.Instance.UnregisterPacket<LiteNetLibWrapper<T>>();
    }

    public void DisconnectPeer(int peerId) { }

    public void SendData<T>(ref T packet, DeliveryType method, bool broadcast) where T : IPacket
    {
        LiteNetLibWrapper<T> wrapper = new()
        {
            Payload = packet
        };

        Singleton<IFikaNetworkManager>.Instance.SendData(ref wrapper, (DeliveryMethod)method, broadcast);
    }

    public void SendDataToPeer<T>(ref T packet, DeliveryType method, int id) where T : IPacket
    {
        LiteNetLibWrapper<T> wrapper = new()
        {
            Payload = packet
        };

        Singleton<IFikaNetworkManager>.Instance.SendDataToPeer(ref wrapper, (DeliveryMethod)method, Singleton<IFikaNetworkManager>.Instance.GetPeerById(id));
    }

    public Player GetPlayerByPeerId(int peerId) => Singleton<IFikaNetworkManager>.Instance.GetPeerById(peerId).Player;

    public int GetPeerIdByPlayer(Player player) => (player as FikaPlayer).NetId;
}
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

    public Action OnNetworkCreated      { get;set; }
    public Action OnNetworkDestroyed    { get;set; }
    
    public Action<int> OnPeerConnected  { get; set; }
    public Action<int> OnDisconnected   { get; set; }

    private readonly Action<FikaNetworkManagerCreatedEvent> OnFikaNetworkManagerCreatedEvent;
    private readonly Action<FikaNetworkManagerDestroyedEvent> OnFikaNetworkManagerDestroyedEvent;
    private readonly Action<PeerConnectedEvent> OnPeerConnectedEvent;
    private readonly Action<PeerDisconnectedEvent> OnPeerDisconnectedEvent;

    public FikaBackend()
    {
        OnFikaNetworkManagerCreatedEvent = new Action<FikaNetworkManagerCreatedEvent>(TriggerNetworkCreated);
        SubscribeEvent(OnFikaNetworkManagerCreatedEvent);

        OnFikaNetworkManagerDestroyedEvent = new Action<FikaNetworkManagerDestroyedEvent>(TriggerNetworkDestroyed);
        SubscribeEvent(OnFikaNetworkManagerDestroyedEvent);

        OnPeerConnectedEvent = new Action<PeerConnectedEvent>(TriggerPeerConnected);
        SubscribeEvent(OnPeerConnectedEvent);

        OnPeerDisconnectedEvent = new Action<PeerDisconnectedEvent>(TriggerPeerDisconnected);
        SubscribeEvent(OnPeerDisconnectedEvent);
    }

    public void Dispose()
    {
        UnsubscribeEvent(OnFikaNetworkManagerCreatedEvent);
        UnsubscribeEvent(OnFikaNetworkManagerDestroyedEvent);
        UnsubscribeEvent(OnPeerConnectedEvent);
        UnsubscribeEvent(OnPeerDisconnectedEvent);
    }

    void TriggerNetworkCreated(FikaNetworkManagerCreatedEvent ev) => OnNetworkCreated.Invoke();
    void TriggerNetworkDestroyed(FikaNetworkManagerDestroyedEvent ev) => OnNetworkDestroyed.Invoke();
    void TriggerPeerConnected(PeerConnectedEvent ev) => OnPeerConnected.Invoke(ev.Peer.Id);
    void TriggerPeerDisconnected(PeerDisconnectedEvent ev) => OnDisconnected.Invoke(ev.Peer.Id);

    public void RegisterPacketWarden<T>(Action<T, int> onReceive) where T : IPacket
    {
        Singleton<IFikaNetworkManager>.Instance.RegisterPacket<LiteNetLibWrapper<T>, NetPeer>((wrapper, peer) => onReceive(wrapper.Payload, peer.Id));
    }

    public void UnregisterPacketWarden<T>() where T : IPacket
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
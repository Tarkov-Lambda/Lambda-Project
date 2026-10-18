using System;
using System.Collections.Generic;
using Comfort.Common;
using EFT;
using Fika.Core.Main.Utils;
using Fika.Core.Modding.Events;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib;
using MemoryPack;
using static Fika.Core.Modding.FikaEventDispatcher;

public class FikaBackend : INetworkBackend
{
    public bool IsServer => FikaBackendUtils.IsServer;
    public bool IsClient => FikaBackendUtils.IsClient;
    public bool IsHeadless => FikaBackendUtils.IsHeadless;
    public bool IsOnline => true;

    public Action OnNetworkCreated { get; set; }
    public Action OnNetworkDestroyed { get; set; }

    public Action<int> OnPeerConnected { get; set; }
    public Action<int> OnPeerDisconnected { get; set; }

    private static IFikaNetworkManager Manager => Singleton<IFikaNetworkManager>.Instance;

    private readonly Dictionary<
        uint,
        (Type Type, Action<ArraySegment<byte>, int> Receive)
    > _receivers = new();
    private IFikaNetworkManager _registeredManager;

    private readonly Action<FikaNetworkManagerCreatedEvent> _onNetworkCreated;
    private readonly Action<FikaNetworkManagerDestroyedEvent> _onNetworkDestroyed;
    private readonly Action<PeerConnectedEvent> _onPeerConnected;
    private readonly Action<PeerDisconnectedEvent> _onPeerDisconnected;

    public FikaBackend()
    {
        _onNetworkCreated = _ =>
        {
            RegisterWrapper();
            OnNetworkCreated?.Invoke();
        };
        _onNetworkDestroyed = _ =>
        {
            OnNetworkDestroyed?.Invoke();
            UnregisterWrapper();
        };
        _onPeerConnected = ev => OnPeerConnected?.Invoke(ev.Peer.Id);
        _onPeerDisconnected = ev => OnPeerDisconnected?.Invoke(ev.Peer.Id);

        SubscribeEvent(_onNetworkCreated);
        SubscribeEvent(_onNetworkDestroyed);
        SubscribeEvent(_onPeerConnected);
        SubscribeEvent(_onPeerDisconnected);
    }

    public void Dispose()
    {
        UnsubscribeEvent(_onNetworkCreated);
        UnsubscribeEvent(_onNetworkDestroyed);
        UnsubscribeEvent(_onPeerConnected);
        UnsubscribeEvent(_onPeerDisconnected);

        UnregisterWrapper();
        _receivers.Clear();
    }

    private void RegisterWrapper()
    {
        IFikaNetworkManager manager = Manager;
        if (manager == null || manager == _registeredManager)
            return;

        manager.RegisterPacket<LiteNetLibWrapper, NetPeer>(OnWrapperReceived);
        _registeredManager = manager;
    }

    private void UnregisterWrapper()
    {
        if (_registeredManager == null)
            return;

        if (Manager == _registeredManager)
            _registeredManager.UnregisterPacket<LiteNetLibWrapper>();
        _registeredManager = null;
    }

    // Exceptions must not escape into Fika's receive loop, it would drop the rest of the batch.
    private void OnWrapperReceived(LiteNetLibWrapper wrapper, NetPeer peer)
    {
        if (!wrapper.IsValid || !_receivers.TryGetValue(wrapper.Id, out var receiver))
            return;

        try
        {
            receiver.Receive(wrapper.Payload, peer.Id);
        }
        catch (Exception ex)
        {
            PacketWardenUtils.Log(
                $"PacketWarden failed to handle packet {wrapper.Id} from peer {peer.Id}: {ex}"
            );
        }
    }

    public void RegisterPacketWarden<T>(Action<T, int> onReceive)
        where T : IPacket
    {
        RegisterWrapper();

        uint id = PacketId<T>.Value;

        if (_receivers.TryGetValue(id, out var existing) && existing.Type != typeof(T))
            PacketWardenUtils.Log(
                $"PacketWarden: {typeof(T)} and {existing.Type} share packet id {id}"
            );

        _receivers[id] = (
            typeof(T),
            (payload, peerId) =>
                onReceive(MemoryPackSerializer.Deserialize<T>(payload.AsSpan()), peerId)
        );
    }

    public void UnregisterPacketWarden<T>()
        where T : IPacket
    {
        _receivers.Remove(PacketId<T>.Value);
    }

    public void SendData<T>(ref T packet, DeliveryType method)
        where T : IPacket
    {
        IFikaNetworkManager manager = Manager;
        if (manager == null)
            return;

        var wrapper = LiteNetLibWrapper.Create(ref packet);
        manager.SendData(ref wrapper, (DeliveryMethod)method);
    }

    public void SendDataToPeer<T>(ref T packet, DeliveryType method, int peerId)
        where T : IPacket
    {
        IFikaNetworkManager manager = Manager;
        NetPeer peer = manager?.GetPeerById(peerId);
        if (peer == null)
            return;

        var wrapper = LiteNetLibWrapper.Create(ref packet);
        manager.SendDataToPeer(ref wrapper, (DeliveryMethod)method, peer);
    }

    public void DisconnectPeer(int peerId)
    {
        if (IsServer)
            Manager?.GetPeerById(peerId)?.Disconnect();
    }

    public Player GetPlayerByPeerId(int peerId)
    {
        if (peerId == INetworkBackend.LocalPeerId)
            return PacketWardenUtils.IsInRaid() ? PacketWardenUtils.GameWorld.MainPlayer : null;

        return IsServer ? Manager?.GetPeerById(peerId)?.Player : null;
    }

    public int GetPeerIdByPlayer(Player player)
    {
        if (player == null)
            return INetworkBackend.InvalidPeerId;
        if (player.IsYourPlayer)
            return INetworkBackend.LocalPeerId;

        if (Manager is FikaServer server && server.NetServer != null)
        {
            foreach (NetPeer peer in server.NetServer)
            {
                if (peer.Player == player)
                    return peer.Id;
            }
        }

        return INetworkBackend.InvalidPeerId;
    }
}

using System;
using EFT;

// Single player: the local machine is the server and nothing goes over the wire.
public class LocalBackend : INetworkBackend
{
    public bool IsServer   => true;
    public bool IsClient   => false;
    public bool IsHeadless => false;
    public bool IsOnline   => false;

    public Action OnNetworkCreated          { get; set; }
    public Action OnNetworkDestroyed        { get; set; }
    public Action<int> OnPeerConnected      { get; set; }
    public Action<int> OnPeerDisconnected   { get; set; }

    public void Dispose() { }

    public void RegisterPacketWarden<T>(Action<T, int> onReceive) where T : IPacket { }

    public void UnregisterPacketWarden<T>() where T : IPacket { }

    public void SendData<T>(ref T packet, DeliveryType method) where T : IPacket { }

    public void SendDataToPeer<T>(ref T packet, DeliveryType method, int peerId) where T : IPacket { }

    public void DisconnectPeer(int peerId) { }

    public Player GetPlayerByPeerId(int peerId) => peerId == INetworkBackend.LocalPeerId && H.IsInRaid() ? H.GameWorld.MainPlayer : null;

    public int GetPeerIdByPlayer(Player player) => player != null && player.IsYourPlayer ? INetworkBackend.LocalPeerId : INetworkBackend.InvalidPeerId;
}
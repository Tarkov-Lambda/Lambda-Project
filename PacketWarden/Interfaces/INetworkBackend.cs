using System;
using EFT;

public enum DeliveryType : byte
{
    ReliableUnordered = 0,
    Sequenced = 1,
    ReliableOrdered = 2,
    ReliableSequenced = 3,
    Unreliable = 4,
}

public interface INetworkBackend : IDisposable
{
    public const int LocalPeerId = -1;
    public const int InvalidPeerId = -2;

    bool IsServer { get; }
    bool IsClient { get; }
    bool IsHeadless { get; }
    bool IsOnline { get; }

    Action OnNetworkCreated { get; set; }
    Action OnNetworkDestroyed { get; set; }
    Action<int> OnPeerConnected { get; set; }
    Action<int> OnPeerDisconnected { get; set; }

    void RegisterPacketWarden<T>(Action<T, int> onReceive)
        where T : IPacket;
    void UnregisterPacketWarden<T>()
        where T : IPacket;

    // Client: to the server. Server: to every peer.
    void SendData<T>(ref T packet, DeliveryType method)
        where T : IPacket;
    void SendDataToPeer<T>(ref T packet, DeliveryType method, int peerId)
        where T : IPacket;
    void DisconnectPeer(int peerId);

    Player GetPlayerByPeerId(int peerId);
    int GetPeerIdByPlayer(Player player);
}

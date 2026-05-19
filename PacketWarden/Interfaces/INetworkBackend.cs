using System;
using EFT;
using UnityEngine.UIElements;

public enum DeliveryType : byte
{
    Unreliable = 4,
    ReliableUnordered = 0,
    Sequenced = 1,
    ReliableOrdered = 2,
    ReliableSequenced = 3
}

public interface INetworkBackend : IDisposable
{
    bool IsServer   { get; }
    bool IsClient   { get; }
    bool IsHeadless { get; }
    bool IsOnline   { get; }

    int NetId       { get; }

    Action OnNetworkCreated     { get; set; }
    Action OnNetworkDestroyed   { get; set; }
    Action<int> OnPeerConnected { get; set; }
    Action<int> OnDisconnected  { get; set; }

    void RegisterPacketWarden<T>(Action<T, int> onReceive) where T : IPacket;
    void UnregisterPacketWarden<T>() where T : IPacket;

    void SendData<T>(ref T packet, DeliveryType method, bool broadcast) where T : IPacket;
    void SendDataToPeer<T>(ref T packet, DeliveryType method, int peerId) where T : IPacket;
    void DisconnectPeer(int peerId);

    Player GetPlayerByPeerId(int id);
    int GetPeerIdByPlayer(Player player);
}
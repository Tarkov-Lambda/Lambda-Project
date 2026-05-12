using System;
using EFT;

public enum DeliveryType
{
    Unreliable = 4,
    ReliableUnordered = 0,
    Sequenced = 1,
    ReliableOrdered = 2,
    ReliableSequenced = 3
}

public interface INetworkBackend
{
    bool IsServer { get; }
    bool IsClient { get; }
    bool IsHeadless { get; }
    // int NetId { get; }

    void SendData<T>(ref T packet, DeliveryType method, bool broadcast) where T : IPacket;
    void SendDataToPeer<T>(ref T packet, DeliveryType method, int peerId) where T : IPacket;
    void DisconnectPeer(int peerId);
    void RegisterPacketHandler<T>(Action<T, int> onReceive) where T : IPacket;
    void UnregisterPacketHandler<T>() where T : IPacket;
    Player GetPlayerByPeerId(int id);
    int GetPeerIdByPlayer(Player player);
}
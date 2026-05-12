using System;
using System.Collections.Generic;

public class LocalSPBackend : INetworkBackend
{
    public bool IsServer => true;
    public bool IsClient => false;
    public bool IsHeadless => false;

    private Dictionary<Type, Delegate> _handlers = new();

    public void RegisterPacketHandler<T>(Action<T, int> onReceive) where T : IPacket
    {
        _handlers[typeof(T)] = onReceive;
    }

    public void UnregisterPacketHandler<T>() where T : IPacket
    {
        _handlers.Remove(typeof(T));
    }

    public void SendData<T>(ref T packet, DeliveryType method, bool broadcast) where T : IPacket
    {
        // In SP, sending a packet just immediately routes it to the local receiver.
        // Simulate local loopback:
        if (_handlers.TryGetValue(typeof(T), out var del))
        {
            var handler = (Action<T, int>)del;
            handler(packet, 0); // 0 = local player ID
        }
    }

    public void SendDataToPeer<T>(ref T packet, DeliveryType method, int id) where T : IPacket { }

    public void DisconnectPeer(int peerId) { }
}
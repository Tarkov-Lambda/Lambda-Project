using System;
using System.Reflection;
using Comfort.Common;
using EFT;
using Fika.Core.Main.Players;
using Fika.Core.Main.Utils;
using Fika.Core.Modding.Events;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using HarmonyLib;
using static Fika.Core.Modding.FikaEventDispatcher;

public class FikaBackend : INetworkBackend, IDisposable
{
    public bool IsServer => FikaBackendUtils.IsServer;
    public bool IsClient => FikaBackendUtils.IsClient;
    public bool IsHeadless => FikaBackendUtils.IsHeadless;
    public int NetId => GetFikaNet().NetId;

    public static NetPeer GetNetPeer() => Singleton<NetPeer>.Instance;
    public static IFikaNetworkManager GetFikaNet() => Singleton<IFikaNetworkManager>.Instance;

    private static FieldInfo _netPacketProcessorField;
    public static NetPacketProcessor GetPacketProcessor()
    {
        var net = GetFikaNet();
        if (net == null) return null;

        _netPacketProcessorField ??= AccessTools.Field(net.GetType(), "_packetProcessor");
        return _netPacketProcessorField?.GetValue(net) as NetPacketProcessor;
    }

    private static FieldInfo _netManagerField;
    public static NetManager GetNetManager()
    {
        var net = GetFikaNet();
        if (net == null) return null;

        _netManagerField ??= AccessTools.Field(net.GetType(), "_netServer");
        return _netManagerField?.GetValue(net) as NetManager;
    }

    public FikaBackend() => OnFikaEvent += ManageFikaEvent;
    public virtual void Dispose() => OnFikaEvent -= ManageFikaEvent;

    protected void ManageFikaEvent(FikaEvent fikaEvent)
    {
        if (fikaEvent is FikaNetworkManagerCreatedEvent) PacketHandlerUtils.TriggerNetworkCreated();
        if (fikaEvent is FikaNetworkManagerDestroyedEvent) PacketHandlerUtils.TriggerNetworkDestroyed();
    }

    public void RegisterPacketHandler<T>(Action<T, int> onReceive) where T : IPacket
    {
        GetFikaNet().RegisterPacket<FikaPacketWrapper<T>, NetPeer>(
            (wrapper, peer) => onReceive(wrapper.Payload, peer.Id)
        );
    }

    public void UnregisterPacketHandler<T>() where T : IPacket
    {
        GetFikaNet().UnregisterPacket<FikaPacketWrapper<T>>();
    }

    public void DisconnectPeer(int peerId) { }


    public void SendData<T>(ref T packet, DeliveryType method, bool broadcast) where T : IPacket
    {
        var wrapper = new FikaPacketWrapper<T> { Payload = packet };
        GetFikaNet().SendData(ref wrapper, ToDeliveryType(method), broadcast);
    }

    public void SendDataToPeer<T>(ref T packet, DeliveryType method, int id) where T : IPacket
    {
        var wrapper = new FikaPacketWrapper<T> { Payload = packet };
        GetFikaNet().SendDataToPeer(ref wrapper, ToDeliveryType(method), GetFikaNet().GetPeerById(id));
    }

    public DeliveryMethod ToDeliveryType(DeliveryType type)
    {
        return (DeliveryMethod)(byte)type;
    }
    
    public Player GetPlayerByPeerId(int peerId) => GetFikaNet().GetPeerById(peerId).Player;

    public int GetPeerIdByPlayer(Player player) => (player as FikaPlayer).NetId;
}
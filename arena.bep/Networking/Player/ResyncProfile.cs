using EFT;
using Fika.Core.Main.Players;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using PacketHandler;

namespace ifp.arena.bep.networking;

/// <summary>
/// Two-phase packet for mid-raid inventory resyncing.
///
/// Phase 1 — Request (client → server):
///   IsRequest = true, NetId = target player's NetId.
///   "Server, please send me the authoritative inventory for this player."
///
/// Phase 2 — Response (server → requesting client):
///   IsRequest = false, NetId = same, Profile = server's copy of the player's Profile.
///   Uses the same PutProfile/GetProfile path as Fika's OwnCharacter reconnect packet.
/// </summary>
public struct InventoryResyncPacket : INetSerializable
{
    /// <summary>NetId of the player whose inventory should be resynced.</summary>
    public int NetId;

    /// <summary>True = this is a request to the server. False = this is the server's response.</summary>
    public bool IsRequest;

    /// <summary>
    /// Full profile of the target player, sent only in the server response.
    /// The inventory lives inside Profile.Inventory.
    /// </summary>
    public Profile Profile;

    public void Serialize(NetDataWriter writer)
    {
        writer.Put(NetId);
        writer.Put(IsRequest);

        // Only include profile data in the response direction
        if (!IsRequest)
            writer.PutProfile(Profile);
    }

    public void Deserialize(NetDataReader reader)
    {
        NetId = reader.GetInt();
        IsRequest = reader.GetBool();

        if (!IsRequest)
            Profile = reader.GetProfile();
    }
}

/// <summary>
/// Handles requesting and applying mid-raid authoritative inventory resyncs.
///
/// The flow mirrors Fika's OwnCharacter reconnect logic from OnReconnectPacketReceived,
/// but works on live players without a full reconnect cycle:
///
///   1. Client calls Request(netId)
///      → sends InventoryResyncPacket { IsRequest=true } to server
///
///   2. Server (WhenApproved) serializes player.Profile (which contains the
///      authoritative inventory) and dispatches the response back to the requester.
///      → sends InventoryResyncPacket { IsRequest=false, Profile=... } to that peer
///
///   3. Client (WhenApproved) receives the profile and calls ReplaceInventory
///      on either the ObservedPlayer's controller or the local player's controller.
///
/// Usage:
///   _resyncHandler = new InventoryResyncPacketHandler();
///   _resyncHandler.Request(H.MainPlayer.NetId); // resync own inventory
///   _resyncHandler.Request(someOtherPlayer.NetId); // resync another player's inventory
/// </summary>
public class InventoryResyncPacketHandler : PacketHandler<InventoryResyncPacket>
{
    public InventoryResyncPacketHandler() : base(DeliveryMethod.ReliableOrdered, PacketAuthority.Anyone) { }

    // ─── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Request an authoritative inventory resync for the player with the given NetId.
    /// Can be called from either client or server.
    /// </summary>
    public void Request(int netId)
    {
        DispatchPacket(new InventoryResyncPacket
        {
            NetId = netId,
            IsRequest = true
        });
    }

    // ─── PacketHandler overrides ──────────────────────────────────────────────

    /// <summary>
    /// Don't auto-broadcast. Requests stay server-side; responses go only to the requester.
    /// </summary>
    protected override bool ShouldBroadcastPacket(InventoryResyncPacket packet) => false;

    protected override void WhenApproved(InventoryResyncPacket packet, NetPeer peer)
    {
        if (packet.IsRequest)
        {
            if (!H.IsServer) return;
            HandleRequest(packet, peer);
        }
        else
        {
            HandleResponse(packet);
        }
    }

    private void HandleRequest(InventoryResyncPacket packet, NetPeer requesterPeer)
    {
        var coopHandler = H.FikaNet.CoopHandler;
        if (coopHandler == null)
        {
            D.Log("[InventoryResync] CoopHandler was null when handling request");
            return;
        }

        if (!coopHandler.Players.TryGetValue(packet.NetId, out var player))
        {
            D.Log($"[InventoryResync] No player found for NetId {packet.NetId}");
            return;
        }

        // player.Profile is the server's authoritative copy.
        // Profile.Inventory is populated via the normal inventory operation flow.
        var response = new InventoryResyncPacket
        {
            NetId = packet.NetId,
            IsRequest = false,
            Profile = player.Profile
        };

        // Send only to the peer that asked
        DispatchPacketToPeer(response, requesterPeer);
        D.Log($"[InventoryResync] Sent inventory for NetId {packet.NetId} to peer {requesterPeer.Id}");
    }

    /// <summary>
    /// Client-side: apply the received authoritative profile's inventory to the player.
    /// Mirrors the SetInventory / ReplaceInventory path used in ObservedPlayer.cs.
    /// </summary>
    private void HandleResponse(InventoryResyncPacket packet)
    {
        var coopHandler = H.FikaNet.CoopHandler;
        if (coopHandler == null)
        {
            D.Log("[InventoryResync] CoopHandler was null when handling response");
            return;
        }

        if (!coopHandler.Players.TryGetValue(packet.NetId, out var player))
        {
            D.Log($"[InventoryResync] No player found for NetId {packet.NetId}");
            return;
        }

        if (packet.Profile?.Inventory == null)
        {
            D.Log("[InventoryResync] Received profile had null Inventory");
            return;
        }

        // ReplaceInventory works on both ObservedPlayer (remote peers) and the local player.
        // For ObservedPlayer this is equivalent to calling SetInventory() with a live
        // inventory object instead of a descriptor — same underlying ReplaceInventory call.
        player.InventoryController.ReplaceInventory(packet.Profile.Inventory);

        var playerType = player is ObservedPlayer ? "ObservedPlayer" : "LocalPlayer";
        D.Log($"[InventoryResync] Applied inventory to {playerType} NetId {packet.NetId}");
    }
}

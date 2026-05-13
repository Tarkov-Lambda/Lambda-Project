using EFT;
using EFT.InputSystem;
using EFT.UI;
using Fika.Core.Main.Utils;
using HarmonyLib;
using Lambda.Core.Main;
using MemoryPack;
using PacketWarden.RateLimiting;
using System.Reflection;
using static EFT.Player;

namespace Lambda.Core.Networking;

[MemoryPackable]
public partial struct InventoryResyncPacket : IPacket
{
    [MemoryPackAllowSerialize]
    public Player Player { get; set; }

    [MemoryPackAllowSerialize]
    public InventoryDescriptorClass inventoryDescriptor;

    public bool broadcast;
}

public class InventoryResyncPacketWarden : LambdaPacketWarden<InventoryResyncPacket>
{
    protected override RateLimitConfig ServerRateLimit => RateLimitPresets.LimitByCooldown(5);

    public void Send(Player player, bool broadcast = false)
    {
        var packet = new InventoryResyncPacket
        {
            Player = player,
            broadcast = broadcast
        };

        DispatchPacket(packet);
    }

    public void SendToPeer(Player player, int peerId)
    {
        var packet = new InventoryResyncPacket
        {
            Player = player,
            broadcast = false
        };

        DispatchPacket(packet, peerId);
    }

    protected override bool ValidatePacket(InventoryResyncPacket packet, int peerId, out string rejectionReason)
    {
        if (packet.broadcast == true)
        {
            rejectionReason = "Action Unauthorized";
            return false;
        }

        return base.ValidatePacket(packet, peerId, out rejectionReason);
    }

    protected override void MutateApprovedPacket(ref InventoryResyncPacket packet, int peerId)
    {
        packet.inventoryDescriptor = EFTItemSerializerClass.SerializeItem(packet.Player.Inventory.Equipment, FikaGlobals.SearchControllerSerializer);
    }

    protected override void ProcessApprovedPacket(ref InventoryResyncPacket packet, int peerId)
    {
        MutateApprovedPacket(ref packet, peerId);

        if (packet.broadcast)
        {
            PacketWardenUtils.Network.SendData(ref packet, DeliveryType, true);
        }
        else if (peerId != Network.NetId)
        {
            PacketWardenUtils.Network.SendDataToPeer(ref packet, DeliveryType, peerId);
        }

        if (packet.Player.IsYourPlayer)
        {
            ApplyInternal(packet, peerId);
        }
    }

    protected override void Apply(InventoryResyncPacket packet, int peerId)
    {
        var player = packet.Player;
        if (packet.inventoryDescriptor == null) return;

        if (packet.Player.IsYourPlayer)
        {
            H.MainPlayer.GetComponent<EftGamePlayerOwner>().CloseInventoryIfOpen();
            H.MainPlayer.UnfuckHands();
            if (H.Session.matchState is not MatchState.Cleanup)
            {
                D.Notify("Resetting inventory, please wait...");
            }
        }

        var newInventory = new EFTInventoryClass()
        {
            Equipment = packet.inventoryDescriptor,
        }.ToInventory();

        player.Profile.Inventory = newInventory;
        player.InventoryController.ReplaceInventory(newInventory);

        newInventory.Equipment.CurrentAddress = player.InventoryController.CreateItemAddress();
        newInventory.Stash?.CurrentAddress = player.InventoryController.CreateItemAddress();

        player.InventoryController.Item_0 = newInventory.Equipment;

        player.UpdateVisuals(newInventory.Equipment);

        if (packet.Player.IsYourPlayer)
        {
            if (ItemUiContext.Instance != null)
            {
                ItemUiContext.Instance.Configure(
                    player.InventoryController,
                    player.Profile,
                    ItemUiContext.Instance.Session,
                    ItemUiContext.Instance.Session?.InsuranceCompany,
                    null,
                    player.HealthController,
                    ItemUiContext.Instance.CompoundItem_0,
                    ItemUiContext.Instance.ContextType,
                    ECursorResult.Ignore,
                    null,
                    newInventory.Equipment,
                    player.AbstractQuestControllerClass
                );
            }

            H.MainPlayer.GetComponent<EftGamePlayerOwner>().CloseInventoryIfOpen();
        }

        if (!H.IsHeadless)
        {
            H.MainPlayer.AutoExamineAndSearch(packet.Player.Inventory.Equipment);
            if (packet.Player.IsYourPlayer && H.Session.matchState == MatchState.Cleanup)
            {
                player.ProcessStatus = EProcessStatus.None;
                player.SetFirstAvailableItem((result) => { });
            }
        }
    }
}
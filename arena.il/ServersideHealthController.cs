using EFT;
using EFT.InventoryLogic;
using Fika.Core.Main.Players;
using EFT.HealthSystem;
using ifp.arena.il;


namespace ifp.arena.il.Patches;

public sealed class ServerActiveHealthController
(Profile.ProfileHealthClass healthInfo, Player player, InventoryController inventoryController, SkillManager skillManager)
: ActiveHealthController(player, healthInfo, inventoryController, skillManager)
{
    private readonly ObservedPlayer _observedPlayer = (ObservedPlayer)player;
    private readonly FikaPlayer _fikaPlayer = (FikaPlayer)player;

    public override bool _sendNetworkSyncPackets
    {
        get
        {
            return true;
        }
    }

    public override bool ApplyItem(Item item, EBodyPart bodyPart, float? amount = null)
    {
        return false;
    }

    public override bool ApplyItem(Item item, GStruct382<EBodyPart> bodyPart, float? amount = null)
    {
        return false;
    }

    public override void CancelApplyingItem()
    {
        base.RemoveMedEffect();
    }

    // public float ApplyDamage(EBodyPart bodyPart, float damage, DamageInfoStruct damageInfo)
    // {
    //     return base.ApplyDamage(bodyPart, damage, damageInfo);
    // }

    private static bool ShouldSend(NetworkHealthSyncPacketStruct.ESyncType syncType)
    {
        switch (syncType)
        {
            case NetworkHealthSyncPacketStruct.ESyncType.AddEffect:
            case NetworkHealthSyncPacketStruct.ESyncType.RemoveEffect:
            case NetworkHealthSyncPacketStruct.ESyncType.IsAlive:
            case NetworkHealthSyncPacketStruct.ESyncType.BodyHealth:
            case NetworkHealthSyncPacketStruct.ESyncType.DestroyedBodyPart:
            case NetworkHealthSyncPacketStruct.ESyncType.EffectStrength:
            case NetworkHealthSyncPacketStruct.ESyncType.EffectNextState:
            case NetworkHealthSyncPacketStruct.ESyncType.EffectMedResource:
            case NetworkHealthSyncPacketStruct.ESyncType.EffectStimulatorBuff:
                return true;
            default:
                return false;
        }
    }

    public override void SendNetworkSyncPacket(NetworkHealthSyncPacketStruct packet)
    {
        Plugin.Logger.LogInfo(packet);
        // When the player dies, delegate to the shared corpse-sync setup path
        // if (packet.SyncType == NetworkHealthSyncPacketStruct.ESyncType.IsAlive
        //     && !packet.Data.IsAlive.IsAlive)
        // {
        //     _observedPlayer.SetupCorpseSyncPacket(packet);
        //     return;
        // }

        // if (ShouldSend(packet.SyncType))
        // {
        //     _observedPlayer.CommonPacket.Type = ECommonSubPacketType.HealthSync;
        //     _observedPlayer.CommonPacket.SubPacket = HealthSyncPacket.FromValue(packet);
        //     _observedPlayer.PacketSender.NetworkManager.SendNetReusable(
        //         ref _observedPlayer.CommonPacket, DeliveryMethod.ReliableOrdered, true);
        // }
    }
}
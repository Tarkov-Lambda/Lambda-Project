using System;
using System.Collections.Generic;
using Comfort.Common;
using EFT;
using EFT.HealthSystem;
using EFT.InventoryLogic;
using Fika.Core.Networking;
using Fika.Core.Networking.Packets.Generic;
using Fika.Core.Networking.Packets.Player.Common.SubPackets;
using UnityEngine;

namespace Fika.Core.Main.HostClasses;

public class ServerAuthoritativeHealthController : ActiveHealthController
{
    public ServerAuthoritativeHealthController
    (Player player, Profile.ProfileHealthClass profileHealth, InventoryController inventory, SkillManager skills)
    : base(player, profileHealth, inventory, skills) { }

    public override bool _sendNetworkSyncPackets => true;

    public override void SendNetworkSyncPacket(NetworkHealthSyncPacketStruct packet)
    {

        var fikaPacket = HealthSyncPacket.FromValue(packet);
        Singleton<IFikaNetworkManager>.Instance.SendGenericPacket(
            EGenericSubPacketType.BorderZone,
            fikaPacket,
            true // broadcast
        );
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

    public float ApplyDamage(EBodyPart bodyPart, float damage, DamageInfoStruct damageInfo)
    {
        return base.ApplyDamage(bodyPart, damage, damageInfo);
    }
}
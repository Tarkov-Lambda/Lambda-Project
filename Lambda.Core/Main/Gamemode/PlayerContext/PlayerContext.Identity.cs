using System;
using System.Collections.Generic;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using Lambda.Core.Main.Economy;
using Lambda.Core.Main.Gamemode;
using Lambda.Core.Networking;
using Lambda.Shared.Models;
using PacketWarden.TimeSync;

namespace Lambda.Core.Main;

public partial class PlayerContext
{
    public void ChangeReadiness(PlayerReadinessState readyState)
    {
        if (context.Identity.ReadyState != readyState)
        {
            context.Identity.ReadyState = readyState;

            if (H.IsHeadless) return;
            if (player == H.MainPlayer)
                EventBus.OnSelfReadinessChanged?.Invoke(readyState);
        }

    }

    public void ChangeProgress(float loadingProgress)
    {
        context.Identity.LoadingProgress = loadingProgress;
    }

    public void SetClanTag(string newClanTag)
    {
        if (newClanTag.IsNullOrEmpty()) return;

        newClanTag = newClanTag.ToUpper();
        context.Identity.ClanTag = newClanTag;
    }

    public void ChangeFaction(Faction faction)
    {
        context.Identity.Faction = faction;

        if (H.IsHeadless) return;
        if (player == H.MainPlayer)
        {
            if (faction == Faction.Spectator)
            {
                var damageInfo = new DamageInfoStruct
                {
                    Damage = 1f,
                    BodyPartColliderType = EBodyPartColliderType.RibcageUp
                };
                Singleton<PlayerKilledPacketWarden>.Instance.Send(damageInfo, player, player);
            }
            EventBus.OnSelfFactionChanged?.Invoke(faction);
        }
    }

    public void SetAdmin(bool isAdmin)
    {
        context.Identity.IsAdmin = isAdmin;
    }

    public void SetBuySelection(Dictionary<ShopItem, Item> buySelection)
    {
        BuySelection = buySelection;
    }

    public void SetDefaultItems(Dictionary<EquipmentSlot, Item> defaultItems)
    {
        DefaultEquipment = defaultItems;
    }


    public void SetHardReset()
    {
        context.Economy.ShouldHardReset = true;
    }

    public void SessionReset()
    {
        context.Combat = new();
        context.Economy = new();
    }

    public void RoundReset()
    {
        context.Combat.Damage += RoundDamage; // apply damage to the total counter after round
        context.Combat.RoundDamage = 0;
        context.Combat.RoundHeadshots = 0;
        context.Combat.RoundKills = 0;
        ItemQuantityBoughtInRound.Clear();
    }
}
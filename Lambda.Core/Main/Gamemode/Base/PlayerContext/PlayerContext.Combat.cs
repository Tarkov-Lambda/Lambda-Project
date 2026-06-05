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
    public void Spawn()
    {
        context.Combat.IsAlive = true;
        _deathTimestamp = -1;
        context.Economy.ShouldHardReset = false;
        _damageContributors.Clear();

        if (!H.IsHeadless && player == H.MainPlayer)
        {
            EventBus.OnSelfRespawn?.Invoke();
            // H.MainPlayer.GetComponent<EftGamePlayerOwner>().Mute(false);
        }
    }

    public void Kill()
    {
        if (H.Session.matchState is MatchState.RoundAction or MatchState.RoundPlanted)
        {
            context.Combat.Deaths++;
        }
        context.Combat.IsAlive = false;
        _deathTimestamp = NetworkTime.ServerNowSeconds;

        // if (!H.IsHeadless && player.IsYourPlayer)
        // {
        //     H.MainPlayer.GetComponent<EftGamePlayerOwner>().Mute(true);
        // }
    }

    public void AddDamage(int newDamage)
    {
        context.Combat.RoundDamage += newDamage;
    }

    public void RecordDamageTaken(Player attacker, float damage)
    {
        if (attacker == null || attacker == player) return;

        if (!_damageContributors.ContainsKey(attacker.Id))
            _damageContributors[attacker.Id] = 0f;

        _damageContributors[attacker.Id] += damage;
    }

    public Player GetTopAssist(Player killer)
    {
        int topAssistId = -1;
        float maxDamage = 0f;

        foreach (var kvp in _damageContributors)
        {
            if (killer != null && kvp.Key == killer.Id) continue;

            if (kvp.Value > maxDamage && kvp.Value >= 125f)
            {
                maxDamage = kvp.Value;
                topAssistId = kvp.Key;
            }
        }

        return topAssistId != -1 ? H.GetPlayer(topAssistId) : null;
    }

    public void AddAssist()
    {
        context.Combat.Assists++;
    }

    public void AddFrag(bool isHeadshot)
    {
        context.Combat.Kills++;
        context.Combat.RoundKills++;
        if (isHeadshot)
        {
            context.Combat.Headshots++;
            context.Combat.RoundHeadshots++;
        }
    }

    public void ChangeBombCarryState(bool IsCarryingBomb)
    {
        context.Combat.IsCarryingBomb = IsCarryingBomb;
    }
}
using Lambda.Core.Main.Gamemode;
using Lambda.Core.Networking;
using System.Collections.Generic;
using UnityEngine;
using EFT.InventoryLogic;
using System;
using Comfort.Common;

namespace Lambda.Core.Main.Economy;

public static class EconomyConstants
{
    public const int MAX_MONEY = 16000;

// #if DEBUG
//     public const int START_MONEY = MAX_MONEY / 2;
// #else 
    public const int START_MONEY = 800;
// #endif

    public const int WIN_ELIMINATION = 3250;
    public const int WIN_TIME = 3250;
    public const int WIN_BOMB_DEFUSE = 3500;
    public const int WIN_BOMB_TARGET = 3500;

    public const int LOSS_BONUS_BASE = 1400;
    public const int LOSS_BONUS_INCREMENT = 500;
    public const int LOSS_BONUS_MAX = 3400;
    public const int LOSS_BONUS_PLANT_ADDITION = 800;

    public const int OBJ_PLANT = 300;
    public const int OBJ_DEFUSE = 300;

    public const int KILL_DEFAULT = 300;
}

public class EconomyManager : IDisposable
{
    private Dictionary<Faction, int> _lossCounters = new();

    public EconomyManager()
    {
        _lossCounters[Faction.CT] = 1;
        _lossCounters[Faction.T] = 1;

        Singleton<PlayerKilledPacketWarden>.Instance.AfterPacketApplied += HandleKillReward;
        EventBus.OnBombStateChange += HandleObjectiveReward;
        EventBus.OnRoundActionEnd += HandleRoundEndEconomy;
    }

    public void Dispose()
    {
        Singleton<PlayerKilledPacketWarden>.Instance.AfterPacketApplied -= HandleKillReward;
        EventBus.OnBombStateChange -= HandleObjectiveReward;
        EventBus.OnRoundActionEnd -= HandleRoundEndEconomy;
    }

    public void ResetEconomy()
    {
        _lossCounters[Faction.CT] = 1;
        _lossCounters[Faction.T] = 1;

        foreach (var p in H.Scoreboard.Values)
        {
            p.SetMoney(EconomyConstants.START_MONEY);
        }
    }

    private void HandleKillReward(PlayerKilledPacket packet)
    {
        if (packet.killer == null || packet.Player == null) return;
        if (!H.Scoreboard.TryGetValue(packet.killer.Id, out var killerScore)) return;
        if (packet.killer == packet.Player) return;

        if (killerScore.Faction == packet.Player.Context.Faction)
        {
            killerScore.AddMoney(-300);
        }
        else killerScore.AddMoney(300);
    }

    private int GetWeaponReward(Item weapon)
    {
        if (weapon is KnifeItemClass) return 1500;
        if (weapon is SniperRifleItemClass) return 100;
        if (weapon is ShotgunItemClass) return 900;
        if (weapon is SmgItemClass) return 600;

        return EconomyConstants.KILL_DEFAULT;
    }

    private void HandleObjectiveReward(BombState state)
    {
        if (H.Arena.LastObjectivePlayer == null) return;

        var score = H.Arena.LastObjectivePlayer.Context;
        if (score == null) return;

        if (state == BombState.Exploded)
        {
            score.AddMoney(EconomyConstants.OBJ_PLANT);
        }
        else if (state == BombState.Defused)
        {
            score.AddMoney(EconomyConstants.OBJ_DEFUSE);
        }
    }

    private void HandleRoundEndEconomy(RoundActionPhaseEnd result)
    {
        if (result.winner == Faction.None)
            return;

        Faction winner = result.winner;
        Faction loser = winner == Faction.CT ? Faction.T : Faction.CT;

        if (_lossCounters[loser] < 4) _lossCounters[loser]++;
        if (_lossCounters[winner] > 0) _lossCounters[winner]--;

        int winReward = CalculateWinReward(result.roundWinReason);
        int lossReward = CalculateLossReward(loser);

        foreach (var p in H.Scoreboard.Values)
        {
            if (p.Faction == Faction.None) continue;

            if (p.Faction == winner)
            {
                p.AddMoney(winReward);
            }
            else
            {
                bool isTerrorist = p.Faction == Faction.T;
                bool survived = p.IsAlive;
                bool bombWasPlanted = H.Session.bombState == BombState.Planted || H.Session.bombState == BombState.Exploded;

                bool isSavingPenalty = isTerrorist && survived && !bombWasPlanted && result.roundWinReason == RoundWinReason.Timeout;

                if (isSavingPenalty)
                {
                    // No income
                }
                else
                {
                    p.AddMoney(lossReward);
                }
            }
        }
    }

    private int CalculateWinReward(RoundWinReason reason)
    {
        switch (reason)
        {
            case RoundWinReason.Objective:
                return 3500;
            case RoundWinReason.Elimination:
            case RoundWinReason.Timeout:
            default:
                return 3250;
        }
    }

    private int CalculateLossReward(Faction losingFaction)
    {
        int count = _lossCounters[losingFaction];

        int reward = EconomyConstants.LOSS_BONUS_BASE + ((Mathf.Clamp(count, 1, 5) - 1) * EconomyConstants.LOSS_BONUS_INCREMENT);

        if (losingFaction == Faction.T && (H.Session.bombState == BombState.Planted || H.Session.bombState == BombState.Defused))
        {
            reward += EconomyConstants.LOSS_BONUS_PLANT_ADDITION;
        }

        return reward;
    }
}

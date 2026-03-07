using ifp.arena.bep.Core.Gamemode;
using ifp.arena.bep.GameTypes;
using ifp.arena.shared;
using ifp.arena.bep.networking; // Assuming PlayerKilledPacket is here
using System.Collections.Generic;
using UnityEngine;
using EFT.InventoryLogic;
using System;

namespace ifp.arena.bep.Core.Economy
{
    public static class EconomyConstants
    {
        public const int MAX_MONEY = 16000;
        public const int START_MONEY = 800;

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
        private Dictionary<Faction, int> _lossCounters = new Dictionary<Faction, int>();

        public EconomyManager()
        {
            _lossCounters[Faction.CT] = 1;
            _lossCounters[Faction.T] = 1;

            // Subscribe to events
            EventBus.OnPlayerKill += HandleKillReward;
            EventBus.OnBombStateChange += HandleObjectiveReward;
            EventBus.OnRoundActionEnd += HandleRoundEndEconomy;

            EventBus.OnEnter += HandleStateChange;
        }

        public void Dispose()
        {
            EventBus.OnPlayerKill -= HandleKillReward;
            EventBus.OnBombStateChange -= HandleObjectiveReward;
            EventBus.OnRoundActionEnd -= HandleRoundEndEconomy;
            EventBus.OnEnter -= HandleStateChange;
        }

        // this is bad and needs to be managed by ArenaController or SND
        private void HandleStateChange(MatchState state)
        {
            if (state == MatchState.WarmupEnd || state == MatchState.SideSwap)
            {
                ResetEconomy();
            }
        }

        public void ResetEconomy()
        {
            _lossCounters[Faction.CT] = 1;
            _lossCounters[Faction.T] = 1;

            foreach (var p in H.Scoreboard.Values)
            {
                p.money = EconomyConstants.START_MONEY;
            }
        }

        private void HandleKillReward(PlayerKilledPacket packet)
        {
            if (!H.Scoreboard.TryGetValue(packet.killerId, out var killerScore)) return;
            if (packet.killerId == packet.victimId) return; // Suicide handled in Round End usually

            int reward = 300;

            // Team Kill Penalty?
            if (killerScore.faction == H.GetPlayerScore(packet.victimId)?.faction)
            {
                reward = -300;
            }

            AddMoney(killerScore, reward);
        }

        private int GetWeaponReward(Weapon weapon)
        {
            if (weapon is KnifeItemClass) return 1500;
            if (weapon is SniperRifleItemClass) return 100; // Snipers (approx)
            if (weapon is ShotgunItemClass) return 900;
            if (weapon is SmgItemClass) return 600; // SMGs

            return EconomyConstants.KILL_DEFAULT; // Rifles, Pistols, etc.
        }

        private void HandleObjectiveReward(BombState state)
        {
            int playerId = H.Arena.LastObjectivePlayerId;
            if (playerId == -1 || !H.Scoreboard.TryGetValue(playerId, out var score)) return;

            if (state == BombState.Planted)
            {
                AddMoney(score, EconomyConstants.OBJ_PLANT);
            }
            else if (state == BombState.Defused)
            {
                AddMoney(score, EconomyConstants.OBJ_DEFUSE);
            }
        }

        private void HandleRoundEndEconomy(RoundActionPhaseEnd result)
        {
            Faction winner = result.winner;
            Faction loser = winner == Faction.CT ? Faction.T : Faction.CT;

            // If you lose, counter goes UP. If you win, counter goes DOWN (soft reset).
            if (_lossCounters[loser] < 4) _lossCounters[loser]++;
            if (_lossCounters[winner] > 0) _lossCounters[winner]--;

            int winReward = CalculateWinReward(result.roundWinReason);
            int lossReward = CalculateLossReward(loser, result.roundWinReason);

            foreach (var p in H.Scoreboard.Values)
            {
                if (p.faction == Faction.None) continue;

                if (p.faction == winner)
                {
                    // Winner always gets paid
                    AddMoney(p, winReward);
                }
                else
                {
                    // Loser Logic
                    bool isTerrorist = p.faction == Faction.T;
                    bool survived = p.isAlive;
                    bool bombWasPlanted = H.Session.bombState == BombState.Planted || H.Session.bombState == BombState.Exploded;

                    // CS2 Rule: Saving as T
                    // If T loses, survives, time ran out (Timeout), and bomb was NOT planted -> $0
                    bool isSavingPenalty = isTerrorist && survived && !bombWasPlanted && result.roundWinReason == RoundWinReason.Timeout;

                    if (isSavingPenalty)
                    {
                        // No income
                    }
                    else
                    {
                        AddMoney(p, lossReward);
                    }
                }
            }
        }

        private int CalculateWinReward(RoundWinReason reason)
        {
            switch (reason)
            {
                case RoundWinReason.Objective: // Bomb Exploded or Defused
                    return 3500;
                case RoundWinReason.Elimination: // Killed all enemies
                case RoundWinReason.Timeout: // CT won by time
                default:
                    return 3250;
            }
        }

        private int CalculateLossReward(Faction losingFaction, RoundWinReason reason)
        {
            int count = _lossCounters[losingFaction];

            int reward = EconomyConstants.LOSS_BONUS_BASE + ((Mathf.Clamp(count, 1, 5) - 1) * EconomyConstants.LOSS_BONUS_INCREMENT);

            if (losingFaction == Faction.T && (H.Session.bombState == BombState.Planted || H.Session.bombState == BombState.Defused))
            {
                reward += EconomyConstants.LOSS_BONUS_PLANT_ADDITION;
            }

            return reward;
        }

        // Helper to cap money
        private void AddMoney(PlayerScore p, int amount)
        {
            p.money += amount;
            if (p.money > EconomyConstants.MAX_MONEY) p.money = EconomyConstants.MAX_MONEY;
            if (p.money < 0) p.money = 0;
            if (p.player == H.MainPlayer)
                EventBus.OnSelfMoneyAdded?.Invoke(amount);
        }
    }
}
using System;
using System.Collections.Generic;
using EFT;
using EFT.InventoryLogic;
using ifp.arena.bep.Core.Economy;
using ifp.arena.bep.Core.Gamemode;
using ifp.arena.bep.GameTypes;
using ifp.arena.bep.networking;
using ifp.arena.bep.networking.TimeSync;
using ifp.arena.shared;
using ifp.arena.shared.Models;

public class PlayerScore
{
    public readonly Player player;

    private PlayerScoreInfo score;

    public PlayerScoreInfo Score => score;

    public Dictionary<ShopItem, Item> BuySelection { get; private set; }

    public Faction Faction => score.Faction;

    // meta gaming (previously known as facebook gaming)
    public PlayerReadinessState ReadyState => score.ReadyState;
    public float LoadingProgress => score.loadingProgress;

    public int Ping => score.Ping;
    public bool IsAdmin => score.IsAdmin;

    public bool IsAlive => score.IsAlive;
    public int Money => score.Money;
    public bool shouldHardReset => score.ShouldHardReset;

    // Match Scope
    public int Kills => score.Kills;
    public int Damage => score.Damage;
    public int Headshots => score.Headshots;
    public int Assists => score.Assists;
    public int Deaths => score.Deaths;
    public int Mvps => score.Mvps;

    // Round Scope
    public int RoundDamage => score.RoundDamage;
    public int RoundKills => score.RoundKills;
    public int RoundHeadshots => score.RoundHeadshots;

    // Serverside
    private double _deathTimestamp;

    public double DeathTimestamp => _deathTimestamp;

    public PlayerScore(int id)
    {
        player = H.GetPlayer(id);

        score.Name = player.Profile.Nickname;
        score.Money = EconomyConstants.MAX_MONEY;

        if (H.IsHeadless) return;
        if (H.IsServer && H.MainPlayer.Id == id)
        {
            score.IsAdmin = true;
        }
    }

    public void Apply(PlayerScoreInfo info)
    {
        score = info;
    }

    public void AddFrag(bool isHeadshot)
    {
        score.Kills++;
        score.RoundKills++;
        if (isHeadshot)
        {
            score.Headshots++;
            score.RoundHeadshots++;
        }
    }

    public void ChangeReadiness(PlayerReadinessState readyState)
    {
        if (score.ReadyState != readyState)
        {
            score.ReadyState = readyState;

            if (H.IsHeadless) return;
            if (player == H.MainPlayer)
                EventBus.OnSelfReadinessChanged?.Invoke(readyState);
        }

    }

    public void ChangeProgress(float loadingProgress)
    {
        score.loadingProgress = loadingProgress;
    }

    public void ChangeFaction(Faction faction)
    {
        score.Faction = faction;

        if (H.IsHeadless) return;
        if (player == H.MainPlayer)
        {
            if (faction == Faction.Spectator)
            {
                player.ActiveHealthController.Kill(EDamageType.HotGases);
            }
            EventBus.OnSelfFactionChanged?.Invoke(faction);
        }
    }

    public void SetAdmin(bool isAdmin)
    {
        score.IsAdmin = isAdmin;
    }

    public void SetBuySelection(Dictionary<ShopItem, Item> buySelection)
    {
        BuySelection = buySelection;
    }

    public void AddDamage(int newDamage)
    {
        score.RoundDamage += newDamage;
    }

    public void Kill()
    {
        if (H.Session.matchState is MatchState.RoundAction or MatchState.RoundPlanted)
        {
            score.Deaths++;
        }
        score.IsAlive = false;
        _deathTimestamp = NetworkTime.LocalNowSeconds;
    }

    public void SetHardReset()
    {
        score.ShouldHardReset = true;
    }

    public void Spawn()
    {
        score.IsAlive = true;
        _deathTimestamp = -1;
        score.ShouldHardReset = false;

        if (!H.IsHeadless && player == H.MainPlayer)
            EventBus.OnSelfRespawn?.Invoke();
    }

    public void SessionReset()
    {
        score.Mvps = 0;
        score.Kills = 0;
        score.Damage = 0;
        score.Headshots = 0;
        score.Assists = 0;
        score.Deaths = 0;
        score.IsAlive = true;

        score.RoundDamage = 0; // very stupid but im not tracking this on clients and instead only doing this on server in HandleDamagePacket
        score.RoundHeadshots = 0;
        score.RoundKills = 0;
    }

    public void RoundReset()
    {
        score.Damage += RoundDamage; // apply damage to the total counter after round
        score.RoundDamage = 0;
        score.RoundHeadshots = 0;
        score.RoundKills = 0;
    }

    // Locking out the player from shooting/jumping/moving
    public bool IsControllerPartiallyLocked()
    {
        if (H.IsHeadless) return false;
        if (!H.IsInRaid()) return false;

        var matchState = H.Session.matchState;
        if (matchState is MatchState.WarmupEnd ||
            matchState is MatchState.RoundPrepare ||
            matchState is MatchState.Pause ||
            matchState is MatchState.SideSwap ||
            matchState is MatchState.Cleanup) return true;

        if (!IsAlive && matchState is not MatchState.None) return true;

        return false;
    }

    public void AddMoney(int amount)
    {
        score.Money += amount;

        score.Money = Math.Clamp(Money, 0, EconomyConstants.MAX_MONEY);

        if (H.IsHeadless) return;
        if (player == H.MainPlayer)
            EventBus.OnSelfMoneyChanged?.Invoke(H.MainPlayerScore.Money);
    }

    public void SpendMoney(int amount)
    {
        if (H.Gamemode is IGMBuyable)
        {
            score.Money -= amount;
            if (Money < 0) score.Money = 0;

            if (H.IsHeadless) return;
            EventBus.OnSelfMoneyChanged?.Invoke(H.MainPlayerScore.Money);
        }
    }

    public void SetMoney(int newMoney)
    {
        score.Money = newMoney;
    }

    public bool CanBuy()
    {
        if (H.Gamemode is IGMBuyable buyable)
        {
            if (H.Session.matchState is MatchState.RoundPrepare) return true;
            if (buyable.CanBuyInActivePhase)
            {
                if (H.Session.matchState is MatchState.RoundAction)
                {
                    return H.Arena.StateTimer >= H.Arena.PhaseDurationSeconds - buyable.TimeInActivePhaseToBuy;
                }
            }
        }

        return false;
    }
}
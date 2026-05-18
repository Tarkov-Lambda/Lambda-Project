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
using UnityEngine;

public class PlayerContext
{
    public readonly Player player;

    private PlayerScoreInfo score;

    public PlayerScoreInfo Score => score;

    // server only data about the player for resets/buying
    public Dictionary<ShopItem, Item> BuySelection { get; private set; }
    public Dictionary<EquipmentSlot, Item> DefaultEquipment { get; private set; }
    public Dictionary<ShopItem, int> ItemQuantityBoughtInRound { get; private set; }

    public string Name                     => score.Name;
    public Faction Faction                 => score.Faction;
    public bool IsAdmin                    => score.IsAdmin;
    public PlayerReadinessState ReadyState => score.ReadyState;
    public int Ping                        => score.Ping;
    public float LoadingProgress           => score.LoadingProgress;

    public bool IsAlive                    => score.IsAlive;
    
    public int Kills                       => score.Kills;
    public int Damage                      => score.Damage;
    public int Headshots                   => score.Headshots;
    public int Assists                     => score.Assists;
    public int Deaths                      => score.Deaths;
    
    public int RoundDamage                 => score.RoundDamage;
    public int RoundKills                  => score.RoundKills;
    public int RoundHeadshots              => score.RoundHeadshots;
    
    public int Mvps                        => score.Mvps;
    
    public int Money                       => score.Money;
    public bool ShouldHardReset            => score.ShouldHardReset;

    // Serverside
    private double _deathTimestamp;
    private Dictionary<int, float> _damageContributors = new();

    public double DeathTimestamp => _deathTimestamp;

    public PlayerContext(int id)
    {
        player = H.GetPlayer(id);

        score.Identity.Name = player.Profile.Nickname;
        score.Economy.Money = EconomyConstants.MAX_MONEY;

        ItemQuantityBoughtInRound = [];

        if (H.IsHeadless) return;
        if (H.IsServer && H.MainPlayer.Id == id)
        {
            score.Identity.IsAdmin = true;
        }
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

            if (kvp.Value > maxDamage && kvp.Value >= 25f)
            {
                maxDamage = kvp.Value;
                topAssistId = kvp.Key;
            }
        }

        return topAssistId != -1 ? H.GetPlayer(topAssistId) : null;
    }

    public void AddAssist()
    {
        score.Combat.Assists++;
    }

    public void Apply(PlayerScoreInfo info)
    {
        score = info;
    }

    public void AddFrag(bool isHeadshot)
    {
        score.Combat.Kills++;
        score.Combat.RoundKills++;
        if (isHeadshot)
        {
            score.Combat.Headshots++;
            score.Combat.RoundHeadshots++;
        }
    }

    public void ChangeReadiness(PlayerReadinessState readyState)
    {
        if (score.Identity.ReadyState != readyState)
        {
            score.Identity.ReadyState = readyState;

            if (H.IsHeadless) return;
            if (player == H.MainPlayer)
                EventBus.OnSelfReadinessChanged?.Invoke(readyState);
        }

    }

    public void ChangeProgress(float loadingProgress)
    {
        score.Identity.LoadingProgress = loadingProgress;
    }

    public void ChangeFaction(Faction faction)
    {
        score.Identity.Faction = faction;

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
        score.Identity.IsAdmin = isAdmin;
    }

    public void SetBuySelection(Dictionary<ShopItem, Item> buySelection)
    {
        BuySelection = buySelection;
    }

    public void SetDefaultItems(Dictionary<EquipmentSlot, Item> defaultItems)
    {
        DefaultEquipment = defaultItems;
    }

    public void AddDamage(int newDamage)
    {
        score.Combat.RoundDamage += newDamage;
    }

    public void Kill()
    {
        if (H.Session.matchState is MatchState.RoundAction or MatchState.RoundPlanted)
        {
            score.Combat.Deaths++;
        }
        score.Combat.IsAlive = false;
        _deathTimestamp = NetworkTime.ServerNowSeconds;
    }

    public void SetHardReset()
    {
        score.Economy.ShouldHardReset = true;
    }

    public void Spawn()
    {
        score.Combat.IsAlive = true;
        _deathTimestamp = -1;
        score.Economy.ShouldHardReset = false;
        _damageContributors.Clear(); // <--- ADD THIS LINE

        if (!H.IsHeadless && player == H.MainPlayer)
            EventBus.OnSelfRespawn?.Invoke();
    }

    public void SessionReset()
    {
        score.Combat.Mvps = 0;
        score.Combat.Kills = 0;
        score.Combat.Damage = 0;
        score.Combat.Headshots = 0;
        score.Combat.Assists = 0;
        score.Combat.Deaths = 0;
        score.Combat.IsAlive = true;

        score.Combat.RoundDamage = 0; // very stupid but im not tracking this on clients and instead only doing this on server in HandleDamagePacket
        score.Combat.RoundHeadshots = 0;
        score.Combat.RoundKills = 0;
    }

    public void RoundReset()
    {
        score.Combat.Damage += RoundDamage; // apply damage to the total counter after round
        score.Combat.RoundDamage = 0;
        score.Combat.RoundHeadshots = 0;
        score.Combat.RoundKills = 0;
        ItemQuantityBoughtInRound.Clear();
    }

    public bool HasReachedLimit(ShopItem shopItem)
    {
        if (shopItem.maxBuy <= 0) return false;

        if (ItemQuantityBoughtInRound.TryGetValue(shopItem, out int count))
        {
            return count >= shopItem.maxBuy;
        }
        return false;
    }

    public void AddItemQuantity(ShopItem shopItem)
    {
        if (ItemQuantityBoughtInRound.ContainsKey(shopItem))
        {
            ItemQuantityBoughtInRound[shopItem]++;
        }
        else
        {
            ItemQuantityBoughtInRound[shopItem] = 1;
        }
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
        score.Economy.Money += amount;

        score.Economy.Money = Math.Clamp(Money, 0, EconomyConstants.MAX_MONEY);

        if (H.IsHeadless) return;
        if (player == H.MainPlayer)
            EventBus.OnSelfMoneyChanged?.Invoke(H.MainPlayerScore.Money);
    }

    public void SpendMoney(int amount)
    {
        if (H.Gamemode is IGMBuyable)
        {
            score.Economy.Money -= amount;
            if (Money < 0) score.Economy.Money = 0;

            if (H.IsHeadless) return;
            EventBus.OnSelfMoneyChanged?.Invoke(H.MainPlayerScore.Money);
        }
    }

    public void SetMoney(int newMoney)
    {
        score.Economy.Money = newMoney;
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
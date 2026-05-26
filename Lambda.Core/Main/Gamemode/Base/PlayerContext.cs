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

public class PlayerContext
{
    public readonly Player player;

    private PlayerContextInfo context;

    public PlayerContextInfo Context => context;

    // server only data about the player for resets/buying
    public Dictionary<ShopItem, Item> BuySelection { get; private set; }
    public Dictionary<EquipmentSlot, Item> DefaultEquipment { get; private set; }
    public Dictionary<ShopItem, int> ItemQuantityBoughtInRound { get; private set; }

    public string Name => context.Name;
    public string ClanTag => context.ClanTag;
    public Faction Faction => context.Faction;
    public bool IsAdmin => context.IsAdmin;
    public PlayerReadinessState ReadyState => context.ReadyState;
    public int Ping => context.Ping;
    public float LoadingProgress => context.LoadingProgress;

    public bool IsAlive => context.IsAlive;

    public int Kills => context.Kills;
    public int Damage => context.Damage;
    public int Headshots => context.Headshots;
    public int Assists => context.Assists;
    public int Deaths => context.Deaths;

    public int RoundDamage => context.RoundDamage;
    public int RoundKills => context.RoundKills;
    public int RoundHeadshots => context.RoundHeadshots;

    public int Mvps => context.Mvps;

    public int Money => context.Money;
    public bool ShouldHardReset => context.ShouldHardReset;

    // Serverside
    private double _deathTimestamp;
    private Dictionary<int, float> _damageContributors = new();

    public double DeathTimestamp => _deathTimestamp;

    public PlayerContext(int id)
    {
        player = H.GetPlayer(id);
        context.Identity.Name = player.Profile.Nickname;
        context.Identity.ClanTag = "";
        context.Economy.Money = EconomyConstants.MAX_MONEY;

        ItemQuantityBoughtInRound = [];

        if (H.IsHeadless) return;
        if (H.IsServer && H.MainPlayer.Id == id)
        {
            context.Identity.IsAdmin = true;
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

    public void Apply(PlayerContextInfo info)
    {
        context = info;
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

    public void AddDamage(int newDamage)
    {
        context.Combat.RoundDamage += newDamage;
    }

    public void Kill()
    {
        if (H.Session.matchState is MatchState.RoundAction or MatchState.RoundPlanted)
        {
            context.Combat.Deaths++;
        }
        context.Combat.IsAlive = false;
        _deathTimestamp = NetworkTime.ServerNowSeconds;

        if (!H.IsHeadless && player == H.MainPlayer)
        {
            H.MainPlayer.GetComponent<EftGamePlayerOwner>().Mute(true);
        }
    }

    public void SetHardReset()
    {
        context.Economy.ShouldHardReset = true;
    }

    public void Spawn()
    {
        context.Combat.IsAlive = true;
        _deathTimestamp = -1;
        context.Economy.ShouldHardReset = false;
        _damageContributors.Clear();

        if (!H.IsHeadless && player == H.MainPlayer)
        {
            EventBus.OnSelfRespawn?.Invoke();
            H.MainPlayer.GetComponent<EftGamePlayerOwner>().Mute(false);
        }
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
        if (!H.IsArenaReady) return false;

        var matchState = H.Session.matchState;
        if (matchState
            is MatchState.WarmupEnd
            or MatchState.RoundPrepare
            or MatchState.Pause
            or MatchState.SideSwap
            or MatchState.Cleanup) return true;

        if (!IsAlive && matchState is not MatchState.None) return true;

        return false;
    }

    public void AddMoney(int amount)
    {
        context.Economy.Money += amount;

        context.Economy.Money = Math.Clamp(Money, 0, EconomyConstants.MAX_MONEY);

        if (H.IsHeadless) return;
        if (player == H.MainPlayer)
            EventBus.OnSelfMoneyChanged?.Invoke(H.MainPlayerScore.Money);
    }

    public void SpendMoney(int amount)
    {
        if (H.Gamemode is IGMBuyable)
        {
            context.Economy.Money -= amount;
            if (Money < 0) context.Economy.Money = 0;

            if (H.IsHeadless) return;
            EventBus.OnSelfMoneyChanged?.Invoke(H.MainPlayerScore.Money);
        }
    }

    public void SetMoney(int newMoney)
    {
        context.Economy.Money = newMoney;
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
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
        context.Identity.ClanTag = string.Empty;
        context.Economy.Money = EconomyConstants.MAX_MONEY;

        ItemQuantityBoughtInRound = [];

        if (H.IsHeadless) return;
        if (H.IsServer && H.MainPlayer.Id == id)
        {
            context.Identity.IsAdmin = true;
        }
    }

    public void Apply(PlayerContextInfo info)
    {
        context = info;
    }

    // Locking out the player from shooting/jumping/moving
    // this needs to go elsewhere
    public bool IsControllerPartiallyLocked()
    {
        if (H.IsHeadless) return false;
        if (!H.IsArenaReady) return false;

        var matchState = H.Session.matchState;
        if (matchState
            is MatchState.WarmupEnd
            or MatchState.RoundPrepare
            // or MatchState.Pause
            or MatchState.SideSwap
            or MatchState.Cleanup) return true;

        if (!IsAlive && matchState is not MatchState.None) return true;

        return false;
    }
}
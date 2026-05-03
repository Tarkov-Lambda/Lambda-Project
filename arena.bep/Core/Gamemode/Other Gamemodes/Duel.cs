using Comfort.Common;
using Cysharp.Threading.Tasks;
using EFT;
using EFT.InventoryLogic;
using Fika.Core.Main.Utils;
using ifp.arena.bep.Core.Economy;
using ifp.arena.bep.GameTypes;
using ifp.arena.bep.networking;
using ifp.arena.shared;
using ifp.arena.shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace ifp.arena.bep.Core.Gamemode;

public class DuelPrepare : SharedPrepare
{
    public override void OnEnter()
    {
        if (!H.IsHeadless)
        {
            // if (H.MainPlayer.GetSlotItem(EquipmentSlot.FirstPrimaryWeapon) == null)
            // {
            //     SniperRifleItemClass TRG = InventoryResetter.GetFirstSniperRifleItem();
            //     BuyMenuSelection.TryGetItemData(TRG.TemplateId, out ShopItem carbineShopInfo);
            //     Purchasing.BuyItem(carbineShopInfo);
            // }
        }

        base.OnEnter();
    }
}

public class DuelAction : IGameState
{
    public MatchState StateType => MatchState.RoundAction;
    public void OnEnter() { }
    public MatchState? OnUpdate()
    {
        if (!H.IsServer) return null;
        Faction? winner = CheckWipe();
        if (winner.HasValue)
        {
            H.Arena.Award(winner.Value, RoundWinReason.Elimination);
            return MatchState.RoundEnd;
        }

        if (H.Arena.StateTimer <= 0)
        {
            H.Arena.Award(Faction.CT, RoundWinReason.Timeout);
            return MatchState.RoundEnd;
        }

        return null;
    }
    public void OnExit() { }

    private Faction? CheckWipe()
    {
        var alive = H.Scoreboard.Values.Where(p => p.IsAlive).GroupBy(p => p.Faction).ToDictionary(g => g.Key, g => g.Count());
        var factions = H.Scoreboard.Values.Select(p => p.Faction).Where(f => f != Faction.None && f != Faction.Spectator).Distinct();

        foreach (var f in factions)
        {
            if (!alive.ContainsKey(f) || alive[f] == 0)
            {
                return factions.FirstOrDefault(o => o != f);
            }
        }

        return null;
    }
}

public class DuelGamemode : LambdaGamemode, IGMRound, IGMTeam
{
    public int MaxRoundsToWin { get; set; } = 13;

    public override IGameState CreateState(MatchState state) => state switch
    {
        MatchState.None         => new SharedNone(),
        MatchState.Warmup       => new SharedWarmup(),
        MatchState.WarmupEnd    => new SharedWarmupEnd(),
        MatchState.Cleanup      => new SharedCleanup(),
        MatchState.Pause        => new SharedPause(),
        MatchState.RoundPrepare => new DuelPrepare(),
        MatchState.RoundAction  => new DuelAction(),
        MatchState.RoundEnd     => new SharedRoundEnd(),
        MatchState.MatchEnd     => new SharedMatchEnd(),
        _ => null
    };
}

using Comfort.Common;
using Cysharp.Threading.Tasks;
using EFT;
using EFT.InventoryLogic;
using Fika.Core.Main.Utils;
using ifp.arena.bep.GameTypes;
using ifp.arena.bep.networking;
using ifp.arena.shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace ifp.arena.bep.Core.Gamemode;

public class SND_Prepare : SharedPrepare
{
    public override void OnEnter()
    {
        foreach (var bombPlantZone in UnityEngine.Object.FindObjectsByType<BombPlantZone>(FindObjectsSortMode.None))
        {
            bombPlantZone.GetComponent<BoxCollider>().enabled = true;
        }

        H.Session.bombState = BombState.None;

        H.Arena.LastObjectivePlayerId = -1;
        H.Arena.LastObjectiveBombState = BombState.None;

        // Hide any leftover bomb visual from the previous round
        H.BombHandler?.SetBombVisuals(new BombStatePacket { state = BombState.None });

        if (!H.IsHeadless)
        {
            var backpack = H.MainPlayer.GetSlotItem(EquipmentSlot.Backpack);
            if (backpack != null)
            {
                Singleton<ForceRemoveItemPacketHandler>.Instance.Send(backpack);
            }
            // H.MainPlayer.TryPopContainedItem(EquipmentSlot.Backpack, true).Forget();
        }

        base.OnEnter();
    }

    public override void OnExit()
    {
        base.OnExit();
    }

}

public class SND_Action : IGameState
{
    public MatchState StateType => MatchState.RoundAction;
    public void OnEnter() { }
    public MatchState? OnUpdate()
    {
        if (!H.IsServer) return null;
        Faction? winner = CheckWipe();
        if (winner.HasValue) { H.Arena.Award(winner.Value, RoundWinReason.Elimination); return MatchState.RoundEnd; }
        if (H.Session.bombState == BombState.Planted) return MatchState.RoundPlanted;
        if (H.Arena.StateTimer <= 0) { H.Arena.Award(Faction.CT, RoundWinReason.Timeout); return MatchState.RoundEnd; }
        return null;
    }
    public void OnExit() { }

    private Faction? CheckWipe()
    {
        var alive = H.Scoreboard.Values.Where(p => p.IsAlive).GroupBy(p => p.Faction).ToDictionary(g => g.Key, g => g.Count());
        var factions = H.Scoreboard.Values.Select(p => p.Faction).Where(f => f != Faction.None && f != Faction.Spectator).Distinct();
        foreach (var f in factions) if (!alive.ContainsKey(f) || alive[f] == 0) return factions.FirstOrDefault(o => o != f);
        return null;
    }
}

public class SND_Planted : IGameState
{
    public MatchState StateType => MatchState.RoundPlanted;

    public void OnEnter() { }

    public MatchState? OnUpdate()
    {
        if (!H.IsServer) return null;

        // If all CT are dead before timer runs out
        // if (!H.Scoreboard.Values.Any(p => p.isAlive && p.faction == Faction.CT))
        // {
        //     H.Arena.Award(Faction.T, RoundWinReason.Elimination);
        //     return MatchState.RoundEnd;
        // }

        if (H.Session.bombState == BombState.Defused)
        {
            H.Arena.Award(Faction.CT, RoundWinReason.Objective);
            return MatchState.RoundEnd;
        }

        if (H.Arena.StateTimer <= 0)
        {
            H.Arena.Award(Faction.T, RoundWinReason.Objective);
            Player lastObjectivePlayer = H.GetPlayer(H.Arena.LastObjectivePlayerId);
            Singleton<BombStatePacketHandler>.Instance.Send(lastObjectivePlayer, BombState.Exploded, Vector3.zero);
            return MatchState.RoundEnd;
        }

        return null;
    }

    public void OnExit() { }
}

public class SND_End : SharedEnd
{
    public override void OnExit()
    {
        int currentRound = H.Session.factionWins.Values.Sum();
        int maxRounds = SND_ModeRules.maxRoundsToWin * 2 - 1;
        double minutes = TimeOfDayHelper.GetMinutesForRound(currentRound, maxRounds);
        Singleton<WeatherAndTimePacketHandler>.Instance.Send((int)minutes);
        base.OnExit();
    }
}

public class SND_SideSwap : SharedSideSwap
{
    public override void OnExit()
    {
        (H.Arena.ActiveRules as SND_ModeRules).hasSideSwapped = true;
        base.OnExit();
    }
}

public class SND_ModeRules : GameModeRules
{
    public static int maxRoundsToWin = 13;
    public static float platingTime = 4.5f;
    public static float defusingTime = 10f;
    public static float defuseRadius = 2.5f;
    public static string bombTemplateId = "628bc7fb408e2b2e9c0801b1";
    public static string defuseKitTemplateId = "544fb5454bdc2df8738b456a";

    public bool hasSideSwapped;

    public SND_ModeRules()
    {
        hasSideSwapped = false;
    }

    public override IGameState CreateState(MatchState state) => state switch
    {
        MatchState.None => new SharedNone(),

        MatchState.Warmup => new SharedWarmup(),
        MatchState.WarmupEnd => new SharedWarmupEnd(),

        MatchState.Pause => new SharedPause(),
        MatchState.RoundPrepare => new SND_Prepare(),
        MatchState.RoundAction => new SND_Action(),
        MatchState.RoundPlanted => new SND_Planted(),
        MatchState.RoundEnd => new SharedEnd(),

        MatchState.SideSwap => new SND_SideSwap(),
        MatchState.MatchEnd => new SharedFinish(),
        _ => null
    };
}

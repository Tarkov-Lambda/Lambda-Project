using Cysharp.Threading.Tasks;
using System.Linq;

namespace Lambda.Core.Main.Gamemode;

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

public class DuelAction : AbstractMatchStateController
{
    public override MatchState StateType => MatchState.RoundAction;
    public override void OnEnter() { }
    public override MatchState? OnUpdate()
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
    public override void OnExit() { }

    private Faction? CheckWipe()
    {
        int aliveCT = 0;
        int aliveT = 0;
        foreach (var p in H.Scoreboard.Values)
        {
            if (p.IsAlive && p.Faction == Faction.CT) aliveCT++;
            if (p.IsAlive && p.Faction == Faction.T) aliveT++;
        }

        if (aliveCT == 0) return Faction.T;
        if (aliveT == 0) return Faction.CT;
        return null;
    }
}

public class DuelGamemode : LambdaGamemode, IGMRound, IGMTeam
{
    public override IInventoryManager InventoryManager => new BaseInventoryManager();

    public override string Name { get; } = "Duel";

    public int MaxRoundsToWin { get; set; } = 13;

    public override AbstractMatchStateController CreateState(MatchState state) => state switch
    {
        MatchState.None => new SharedNone(),
        MatchState.Warmup => new SharedWarmup(),
        MatchState.WarmupEnd => new SharedWarmupEnd(),
        MatchState.Cleanup => new SharedCleanup(),
        MatchState.Pause => new SharedPause(),
        MatchState.RoundPrepare => new DuelPrepare(),
        MatchState.RoundAction => new DuelAction(),
        MatchState.RoundEnd => new SharedRoundEnd(),
        MatchState.MatchEnd => new SharedMatchEnd(),
        _ => null
    };
}

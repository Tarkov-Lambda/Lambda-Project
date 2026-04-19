
using System;
using ifp.arena.bep.GameTypes;
using ifp.arena.bep.networking;
using ifp.arena.shared;
using ifp.arena.shared.Models;

namespace ifp.arena.bep.Core.Gamemode;

public static class EventBus
{
    public static Action<MatchState> OnEnter;
    // We do not have OnUpdate action because clients don't run the update loop 
    public static Action<MatchState> OnExit;
    public static Action<BombState> OnBombStateChange;
    public static Action<PlayerKilledPacket> OnPlayerKill;
    public static Action<RoundActionPhaseEnd> OnRoundActionEnd;

    public static Action<int> OnSelfMoneyChanged;
    public static Action<ShopItem> OnItemBuy;

    public static Action OnSelfRespawn;
    public static Action<PlayerReadinessState> OnSelfReadinessChanged;
    public static Action<Faction> OnSelfFactionChanged;
}
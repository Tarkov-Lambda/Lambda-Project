using System;
using EFT;
using ifp.arena.bep.Core.Economy;
using ifp.arena.bep.Core.Gamemode;
using ifp.arena.bep.GameTypes;
using ifp.arena.bep.networking;
using ifp.arena.shared;
using ifp.arena.shared.Models;

public class PlayerScore
{
    public readonly Player player;

    // this is absolutely fucking retarded but my hand was forced
    public PlayerScoreInformationSChipsami score;

    public Faction Faction => score.faction;

    // meta gaming (previously known as facebook gaming)
    public PlayerReadinessState ReadyState => score.readyState;
    public int Ping => score.ping;
    public bool IsAdmin => score.isAdmin;

    // Round scope
    public int Kills => score.kills;
    public int Damage => score.damage;
    public int Headshots => score.headshots;
    public int Assists => score.assists;
    public int Deaths => score.deaths;
    public int Mvps => score.mvps;

    // only the server knows this value
    public int RoundDamage => score.roundDamage;
    public int RoundKills => score.roundKills;
    public int RoundHeadshots => score.roundHeadshots;
    public bool IsAlive => score.isAlive;
    public int Money => score.money;


    public PlayerScore(int id)
    {
        player = H.GetPlayer(id);

        if (H.IsHeadless) return;
        if (H.IsServer && H.MainPlayer.Id == id)
        {
            score.isAdmin = true;
        }
    }

    public void AddFrag(bool isHeadshot)
    {
        score.kills++;
        score.roundKills++;
        if (isHeadshot)
        {
            score.headshots++;
            score.roundHeadshots++;
        }
    }

    public void ChangeReadiness(PlayerReadinessState readyState)
    {
        score.readyState = readyState;
    }

    public void ChangeFaction(Faction faction)
    {
        score.faction = faction;

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
        score.isAdmin = isAdmin;
    }

    public void AddDamage(int newDamage)
    {
        score.roundDamage += newDamage;
    }

    public void Kill()
    {
        score.deaths++;
        score.isAlive = false;
    }

    public void Spawn()
    {
        score.isAlive = true;
        if (!H.IsHeadless && player == H.MainPlayer)
            EventBus.OnSelfRespawn?.Invoke();
    }

    public void SessionReset()
    {
        score.mvps = 0;
        score.kills = 0;
        score.damage = 0;
        score.headshots = 0;
        score.assists = 0;
        score.deaths = 0;
        score.isAlive = true;

        score.roundDamage = 0; // very stupid but im not tracking this on clients and instead only doing this on server in HandleDamagePacket
        score.roundHeadshots = 0;
        score.roundKills = 0;
    }

    public void RoundReset()
    {
        score.damage += RoundDamage; // apply damage to the total counter after round
        score.roundDamage = 0;
        score.roundHeadshots = 0;
        score.roundKills = 0;
    }

    public void Sync(PlayerScoreSyncData packet)
    {
        var newFaction = (Faction)packet.faction;
        bool factionChanged = newFaction != Faction;
        score.faction = newFaction;

        // Mirror the event that ChangeFaction fires on the server, so clients get
        // the side-swap notification without needing to go through ChangeFaction().
        if (factionChanged && !H.IsHeadless && player == H.MainPlayer)
            EventBus.OnSelfFactionChanged?.Invoke(Faction);

        score.mvps = packet.mvps;
        score.kills = packet.kills;
        score.headshots = packet.headshots;
        score.assists = packet.assists;
        score.deaths = packet.deaths;
        score.money = packet.money;
        score.isAlive = packet.isAlive;
        score.readyState = packet.readyState;

        score.roundKills = packet.roundKills;
        score.roundHeadshots = packet.roundHeadshots;
    }

    public void AddMoney(int amount)
    {
        score.money += amount;

        score.money = Math.Clamp(Money, 0, EconomyConstants.MAX_MONEY);

        if (H.IsHeadless) return;
        if (player == H.MainPlayer)
            EventBus.OnSelfMoneyChanged?.Invoke(H.MainPlayerScore.Money);
    }

    public void SpendMoney(int amount)
    {
        score.money -= amount;
        if (Money < 0)
            score.money = 0;

        if (H.IsHeadless) return;
        EventBus.OnSelfMoneyChanged?.Invoke(H.MainPlayerScore.Money);
    }

    public void SetMoney(int newMoney)
    {
        score.money = newMoney;
    }

    public bool CanBuy()
    {
        if (H.Session.matchState is MatchState.RoundPrepare) return true;
        if (H.Session.matchState is MatchState.RoundAction)
            return H.Arena.StateTimer >= H.Arena.PhaseDurationSeconds - 30; // only allow buying within first 30 seconds of round action

        return false;
    }
}
using System;
using EFT;
using ifp.arena.bep.Core.Economy;
using ifp.arena.bep.Core.Gamemode;
using ifp.arena.bep.GameTypes;
using ifp.arena.bep.networking;
using ifp.arena.shared;

public class PlayerScore
{
    public readonly Player player;

    public Faction Faction { get; private set; }

    // meta gaming (previously known as facebook gaming)
    public PlayerReadinessState readyState;
    public int ping;
    public bool isAdmin;

    // Round scope
    public int Kills { get; private set; }
    public int Damage { get; private set; }
    public int Headshots { get; private set; }
    public int Assists { get; private set; }
    public int Deaths { get; private set; }
    public int Mvps { get; private set; }

    // only the server knows this value
    public int RoundDamage { get; private set; }

    public int RoundKills { get; private set; }
    public int RoundHeadshots { get; private set; }

    public bool IsAlive { get; private set; }
    public int Money { get; private set; }


    public PlayerScore(int id)
    {
        player = H.GetPlayer(id);
        
        if(H.IsHeadless) return;
        if (H.IsServer && H.MainPlayer.Id == id)
        {
            isAdmin = true;
        }
    }

    public void AddFrag(bool isHeadshot)
    {
        Kills++;
        RoundKills++;
        if (isHeadshot)
        {
            Headshots++;
            RoundHeadshots++;
        }
    }

    public void ChangeFaction(Faction faction)
    {
        this.Faction = faction;

        if(H.IsHeadless) return;
        if (player == H.MainPlayer)
            EventBus.OnSelfFactionChanged?.Invoke(faction);
    }

    public void AddDamage(int newDamage)
    {
        RoundDamage += newDamage;
    }

    public void Kill()
    {
        Deaths++;
        IsAlive = false;
    }

    public void Spawn()
    {
        IsAlive = true;
        EventBus.OnSelfRespawn?.Invoke();
    }

    public void SessionReset()
    {
        Mvps = 0;
        Kills = 0;
        Damage = 0;
        Headshots = 0;
        Assists = 0;
        Deaths = 0;
        IsAlive = true;

        RoundDamage = 0; // very stupid but im not tracking this on clients and instead only doing this on server in HandleDamagePacket
        RoundHeadshots = 0;
        RoundKills = 0;
    }

    public void RoundReset()
    {
        Damage += RoundDamage; // apply damage to the total counter after round
        RoundDamage = 0;
        RoundHeadshots = 0;
        RoundKills = 0;
    }

    public void Sync(PlayerScoreSyncData packet)
    {
        Faction = (Faction)packet.faction;
        Mvps = packet.mvps;
        Kills = packet.kills;
        Headshots = packet.headshots;
        Assists = packet.assists;
        Deaths = packet.deaths;
        Money = packet.money;
        IsAlive = packet.isAlive;
        readyState = packet.readyState;

        RoundKills = packet.roundKills;
        RoundHeadshots = packet.roundHeadshots;
    }

    public void AddMoney(int amount)
    {
        Money += amount;

        Money = Math.Clamp(Money, 0, EconomyConstants.MAX_MONEY);

        if(H.IsHeadless) return;
        if (player == H.MainPlayer)
            EventBus.OnSelfMoneyChanged?.Invoke(H.MainPlayerScore.Money);
    }

    public void SpendMoney(int amount)
    {
        Money -= amount;
        if (Money < 0)
            Money = 0;
            
        if(H.IsHeadless) return;
        EventBus.OnSelfMoneyChanged?.Invoke(H.MainPlayerScore.Money);
    }

    public void SetMoney(int newMoney)
    {
        Money = newMoney;
    }

    public bool CanBuy()
    {
        if (H.Session.matchState is MatchState.RoundPrepare) return true;
        if (H.Session.matchState is MatchState.RoundAction)
            return H.Arena.StateTimer >= H.Arena.PhaseDurationSeconds - 30; // only allow buying within first 30 seconds of round action

        return false;
    }
}
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

    public Faction faction { get; private set; }

    // Round scope
    public int kills { get; private set; }
    public int damage { get; private set; }
    public int headshots { get; private set; }
    public int assists { get; private set; }
    public int deaths { get; private set; }
    public int mvps { get; private set; }

    // only the server knows this value
    public int s_roundDamage { get; private set; }

    public int roundKills { get; private set; }
    public int roundHeadshots { get; private set; }

    public bool isAlive { get; private set; }
    public int money { get; private set; }

    // meta gaming (previously known as facebook gaming)
    public PlayerReadinessState readyState;
    public int ping;
    public bool IsAdmin;

    public PlayerScore(int id)
    {
        player = H.GetPlayer(id);
        if (H.IsServer && H.MainPlayer.Id == id)
        {
            IsAdmin = true;
        }
    }

    public void AddFrag(bool isHeadshot)
    {
        kills++;
        roundKills++;
        if (isHeadshot)
        {
            headshots++;
            roundHeadshots++;
        }
    }

    public void ChangeFaction(Faction faction)
    {
        this.faction = faction;
        if (player == H.MainPlayer)
            EventBus.OnSelfFactionChanged?.Invoke(faction);
    }

    public void AddDamage(int newDamage)
    {
        s_roundDamage += newDamage;
    }

    public void Kill()
    {
        deaths++;
        isAlive = false;
    }

    public void Spawn()
    {
        isAlive = true;
        EventBus.OnSelfRespawn?.Invoke();
    }

    public void SessionReset()
    {
        mvps = 0;
        kills = 0;
        damage = 0;
        headshots = 0;
        assists = 0;
        deaths = 0;
        isAlive = true;

        s_roundDamage = 0; // very stupid but im not tracking this on clients and instead only doing this on server in HandleDamagePacket
        roundHeadshots = 0;
        roundKills = 0;
    }

    public void RoundReset()
    {
        damage += s_roundDamage; // apply damage to the total counter after round
        s_roundDamage = 0;
        roundHeadshots = 0;
        roundKills = 0;
    }

    public void Sync(PlayerScoreSyncData packet)
    {
        faction = (Faction)packet.faction;
        mvps = packet.mvps;
        kills = packet.kills;
        headshots = packet.headshots;
        assists = packet.assists;
        deaths = packet.deaths;
        money = packet.money;
        isAlive = packet.isAlive;
        readyState = packet.readyState;

        roundKills = packet.roundKills;
        roundHeadshots = packet.roundHeadshots;
    }

    public void AddMoney(int amount)
    {
        money += amount;

        money = Math.Clamp(money, 0, EconomyConstants.MAX_MONEY);

        if (player == H.MainPlayer)
            EventBus.OnSelfMoneyChanged?.Invoke(H.MainPlayerScore.money);
    }

    public void SpendMoney(int amount)
    {
        money -= amount;
        if (money < 0)
            money = 0;
        EventBus.OnSelfMoneyChanged?.Invoke(H.MainPlayerScore.money);
    }

    public void SetMoney(int newMoney)
    {
        money = newMoney;
    }

    public bool CanBuy()
    {
        if (H.Session.matchState is MatchState.RoundPrepare) return true;
        if (H.Session.matchState is MatchState.RoundAction)
            return H.Arena.StateTimer >= H.Arena.PhaseDurationSeconds - 30; // only allow buying within first 30 seconds of round action

        return false;
    }
}
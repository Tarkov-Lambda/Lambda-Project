using MemoryPack;

namespace Lambda.Shared.Models
{
    [System.Serializable]
    [MemoryPackable]
    public partial struct PlayerContextInfo
    {
        public PlayerIdentity Identity;
        // public PlayerLoadingStatus Status;
        public PlayerCombat Combat;
        public PlayerEconomyData Economy;

        // tech debt :C
        public readonly string Name                     => Identity.Name;
        public readonly Faction Faction                 => Identity.Faction;
        public readonly bool IsAdmin                    => Identity.IsAdmin;
        public readonly PlayerReadinessState ReadyState => Identity.ReadyState;
        public readonly int Ping                        => Identity.Ping;
        public readonly float LoadingProgress           => Identity.LoadingProgress;

        public readonly bool IsAlive                    => Combat.IsAlive;

        public readonly int Kills                       => Combat.Kills;
        public readonly int Damage                      => Combat.Damage;
        public readonly int Headshots                   => Combat.Headshots;
        public readonly int Assists                     => Combat.Assists;
        public readonly int Deaths                      => Combat.Deaths;

        public readonly int RoundDamage                 => Combat.RoundDamage;
        public readonly int RoundKills                  => Combat.RoundKills;
        public readonly int RoundHeadshots              => Combat.RoundHeadshots;

        public readonly int Mvps                        => Combat.Mvps;

        public readonly int Money                       => Economy.Money;
        public readonly bool ShouldHardReset            => Economy.ShouldHardReset;
    }

    [System.Serializable]
    [MemoryPackable]
    public partial struct PlayerIdentity
    {
        public string Name;
        public Faction Faction;
        public bool IsAdmin;
        
        public PlayerReadinessState ReadyState;
        public int Ping;
        public float LoadingProgress;
    }

    [System.Serializable]
    [MemoryPackable]
    public partial struct PlayerCombat
    {
        public bool IsAlive;

        public int Kills;
        public int Damage;
        public int Headshots;
        public int Assists;
        public int Deaths;

        public int RoundDamage;
        public int RoundKills;
        public int RoundHeadshots;

        public int Mvps;
    }

    [System.Serializable]
    [MemoryPackable]
    public partial struct PlayerEconomyData
    {
        public int Money;
        public bool ShouldHardReset;
    }
}

using MemoryPack;

namespace ifp.arena.shared.Models
{
    [System.Serializable]
    [MemoryPackable]
    public partial struct PlayerScoreInfo
    {
        public string Name;
        public Faction Faction;

        public PlayerReadinessState ReadyState;
        public float loadingProgress;
        public int Ping;
        public bool IsAdmin;

        public int Kills;
        public int Damage;
        public int Headshots;
        public int Assists;
        public int Deaths;
        public int Mvps;

        public int RoundDamage;
        public int RoundKills;
        public int RoundHeadshots;
        public bool IsAlive;
        public int Money;

        public string[] VkusiChipsov; // VkusiChipsov
        public int SkolkoChipsovOstalos; // SkolkoChipsovOstalos
        public float TimestampSinceAteChipsi; // TimestampSinceAteChipsi
    }

    [System.Serializable]
    [MemoryPackable]
    public partial struct PlayerIdentity
    {
        public string Name;
        public Faction Faction;
        public bool IsAdmin;
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
    public partial struct PlayerLoadingStatus
    {
        public PlayerReadinessState ReadyState;
        public float LoadingProgress;
        public int Ping;
    }
}

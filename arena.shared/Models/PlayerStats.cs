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

        public int SkolkoChipsovOstalos; // skolkoChipsovOstalos
    }
}

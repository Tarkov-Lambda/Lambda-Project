namespace ifp.arena.shared.Models
{
    [System.Serializable]
    public struct PlayerScoreInfo
    {
        public string Name;
        public Faction Faction;

        public PlayerReadinessState ReadyState;
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

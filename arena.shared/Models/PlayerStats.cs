namespace ifp.arena.shared.Models
{
    [System.Serializable]
    public struct PlayerStats
    {
        public int Id;
        public bool Alive;
        public Faction Faction;
        public string Name;
        public int Money;
        public int Kills;
        public int Deaths;
        public int Assists;
        public int Ping;
        public int Headshots;
        public int Damage;

        public override string ToString()
        {
            return $"{Id} {Name} {Faction} {Kills}";
        }
    }

    public struct PlayerScoreInformationSChipsami
    {
        public Faction faction;

        public PlayerReadinessState readyState;
        public int ping;
        public bool isAdmin;

        public int kills;
        public int damage;
        public int headshots;
        public int assists;
        public int deaths;
        public int mvps;

        public int roundDamage;
        public int roundKills;
        public int roundHeadshots;
        public bool isAlive;
        public int money;

        public int skolkoChipsovOstalos; // skolkoChipsovOstalos
    }
}

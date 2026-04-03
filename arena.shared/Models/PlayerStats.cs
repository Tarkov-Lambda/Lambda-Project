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
}

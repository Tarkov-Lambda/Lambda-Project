namespace ifp.arena.shared.Models
{
    [System.Serializable]
    public struct PlayerStats
    {
        public int Id;
        public Faction Faction;
        public string Name;
        public int Kills;
        public int Deaths;
        public int Assists;
        public int Ping;
        public int Headshots;

        public override string ToString()
        {
            return $"{Id} {Name} {Faction} {Kills}";
        }
    }
}

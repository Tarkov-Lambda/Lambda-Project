using System.Collections.Generic;
using ifp.arena.shared;
using ifp.arena.shared.Models;
using UnityEngine;

namespace arena.ui.scoreboard
{
    public class Scoreboard : MonoBehaviour
    {
        [SerializeField] private TeamBoard prefabTeamboard;
        [SerializeField] private RectTransform containerTeams;
        [SerializeField] private FactionColors factionColors;

        private readonly List<TeamBoard> pool = new List<TeamBoard>();
        private readonly Dictionary<Faction, List<PlayerStats>> buckets = new Dictionary<Faction, List<PlayerStats>>();

        public void SetPlayers(PlayerStats[] players, Dictionary<Faction, int> teamScores)
        {
            buckets.Clear();

            foreach (PlayerStats p in players)
            {
                if (!buckets.TryGetValue(p.Faction, out List<PlayerStats> list))
                {
                    list = new List<PlayerStats>();
                    buckets[p.Faction] = list;
                }
                list.Add(p);
            }

            int index = 0;

            foreach (KeyValuePair<Faction, List<PlayerStats>> kvp in buckets)
            {
                TeamBoard board;

                if (index < pool.Count)
                {
                    board = pool[index];
                }
                else
                {
                    board = Instantiate(prefabTeamboard, containerTeams);
                    pool.Add(board);
                }

                teamScores.TryGetValue(kvp.Key, out int score);
                board.gameObject.SetActive(true);
                board.Set(kvp.Value, factionColors.Get(kvp.Key), score);
                index++;
            }

            for (int i = index; i < pool.Count; i++)
                pool[i].gameObject.SetActive(false);
        }
    }
}
using System.Collections.Generic;
using Lambda.Shared;
using Lambda.Shared.Models;
using UnityEngine;

namespace Lambda.UI.scoreboard
{
    public class Scoreboard : MonoBehaviour
    {
        [SerializeField] private TeamBoard prefabTeamboard;
        [SerializeField] private RectTransform containerTeams;
        [SerializeField] private FactionColors factionColors;

        private readonly List<TeamBoard> pool = new();
        private readonly Dictionary<Faction, List<PlayerContextInfo>> buckets = new Dictionary<Faction, List<PlayerContextInfo>>();

        public void SetPlayers(PlayerContextInfo[] players, Dictionary<Faction, int> teamScores, Faction mainPlayerFaction)
        {
            buckets.Clear();

            foreach (var p in players)
            {
                if (!buckets.TryGetValue(p.Faction, out List<PlayerContextInfo> list))
                {
                    list = new List<PlayerContextInfo>();
                    buckets[p.Faction] = list;
                }
                list.Add(p);
            }

            int index = 0;

            foreach (KeyValuePair<Faction, List<PlayerContextInfo>> kvp in buckets)
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
                board.Set(kvp.Value, factionColors.Get(kvp.Key), score, mainPlayerFaction, kvp.Key);
                index++;
            }

            for (int i = index; i < pool.Count; i++)
                pool[i].gameObject.SetActive(false);
        }
    }
}
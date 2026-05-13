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

        private readonly List<TeamBoard> pool = new List<TeamBoard>();
        private readonly Dictionary<Faction, List<PlayerScoreInfo>> buckets = new Dictionary<Faction, List<PlayerScoreInfo>>();

        public void SetPlayers(PlayerScoreInfo[] players, Dictionary<Faction, int> teamScores, Faction mainPlayerFaction)
        {
            buckets.Clear();

            foreach (var p in players)
            {
                if (!buckets.TryGetValue(p.Faction, out List<PlayerScoreInfo> list))
                {
                    list = new List<PlayerScoreInfo>();
                    buckets[p.Faction] = list;
                }
                list.Add(p);
            }

            int index = 0;

            foreach (KeyValuePair<Faction, List<PlayerScoreInfo>> kvp in buckets)
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
                board.Set(kvp.Value, factionColors.Get(kvp.Key), score, mainPlayerFaction);
                index++;
            }

            for (int i = index; i < pool.Count; i++)
                pool[i].gameObject.SetActive(false);
        }
    }
}
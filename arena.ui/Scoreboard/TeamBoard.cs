using System.Collections.Generic;
using ifp.arena.shared;
using ifp.arena.shared.Models;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace arena.ui.scoreboard
{
    public class TeamBoard : MonoBehaviour
    {
        [SerializeField] private GameObject scoreContainer;
        [SerializeField] private TMP_Text textTeamScore;

        [SerializeField] private RowPlayer prefabRowPlayer;
        [SerializeField] private RectTransform containerPlayers;
        [SerializeField] private Graphic[] coloredGraphicsKeepAlpha;

        private readonly List<RowPlayer> pool = new List<RowPlayer>();

        public void Set(List<PlayerScoreInfo> players, Color teamColor, int score, Faction mainPlayerFaction)
        {
            scoreContainer.SetActive(score >= 0);
            textTeamScore.text = score.ToString();

            foreach (var graphic in coloredGraphicsKeepAlpha)
            {
                var c = teamColor;
                c.a = graphic.color.a;
                graphic.color = c;
            }

            for (int i = 0; i < players.Count; i++)
            {
                RowPlayer row;
                if (i < pool.Count)
                {
                    row = pool[i];
                    row.gameObject.SetActive(true);
                }
                else
                {
                    row = Instantiate(prefabRowPlayer, containerPlayers);
                    pool.Add(row);
                }

                row.Set(players[i], players[i].Faction == mainPlayerFaction, i);
            }

            // deactivate surplus rows
            for (int i = players.Count; i < pool.Count; i++)
            {
                pool[i].gameObject.SetActive(false);
            }
        }
    }
}
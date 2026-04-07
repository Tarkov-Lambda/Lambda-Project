using System.Collections.Generic;
using ifp.arena.shared.Models;
using UnityEngine;

namespace arena.ui
{
    public class TeamStatus : MonoBehaviour
    {
        [SerializeField] private TeamStatusPlayer prefabPlayer;
        [SerializeField] private RectTransform container;
        [SerializeField] private FactionColors factionColors;

        private readonly List<TeamStatusPlayer> _playerPool = new List<TeamStatusPlayer>();

        bool init;
        void Init()
        {
            if (init)
                return;
            init = true;

            foreach (Transform item in container)
            {
                GameObject.Destroy(item.gameObject);
            }
        }

        public void Set(PlayerStats[] players)
        {
            Init();
            if (players == null) return;

            while (_playerPool.Count < players.Length)
            {
                TeamStatusPlayer instance = Instantiate(prefabPlayer, container);
                _playerPool.Add(instance);
            }

            for (int i = 0; i < _playerPool.Count; i++)
            {
                TeamStatusPlayer uiPlayer = _playerPool[i];

                if (i < players.Length)
                {
                    uiPlayer.gameObject.SetActive(true);

                    if (players[i].Alive)
                        uiPlayer.SetAlive();
                    else
                        uiPlayer.SetDead();

                    uiPlayer.SetColor(factionColors.Get(players[i].Faction));
                }
                else
                {
                    uiPlayer.gameObject.SetActive(false);
                }
            }
        }
    }
}

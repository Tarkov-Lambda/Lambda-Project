using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if EFT_RUNTIME
using ifp.arena.bep.GameTypes;
#endif

namespace arena.ui.scoreboard
{
    public class Scoreboard : MonoBehaviour
    {
        [SerializeField] private TeamBoard prefabTeamboard;
        [SerializeField] private RectTransform containerTeams;

#if EFT_RUNTIME
        public void SetPlayers(Dictionary<int, PlayerScore> players)
        {

        }
#endif
    }
}

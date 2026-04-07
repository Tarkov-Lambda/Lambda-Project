using arena.ui.killfeed;
using arena.ui.scoreboard;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace arena.ui
{
    public class ArenaMatchUI : MonoBehaviour
    {
        [field: SerializeField] public TopBar TopBar { get; private set; }
        [field: SerializeField] public Scoreboard Scoreboard { get; private set; }
        [field: SerializeField] public KillFeed KillFeed { get; private set; }
        [field: SerializeField] public PopupMatchEnd PopupMatchEnd { get; private set; }
        [field: SerializeField] public DeathInfo DeathInfo { get; private set; }
        [field: SerializeField] public Spectator Spectator { get; private set; }

        public void ToggleScoreboard(bool show)
        {
            Scoreboard.gameObject.SetActive(show);
        }

        public void ToggleSpectator(bool show)
        {
            Spectator.gameObject.SetActive(show);
        }
    }
}

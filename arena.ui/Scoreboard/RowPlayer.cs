using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace arena.ui.scoreboard
{
    public class RowPlayer : MonoBehaviour
    {
        [SerializeField] private TMP_Text textName;
        [SerializeField] private TMP_Text textKills;
        [SerializeField] private TMP_Text textDeaths;
        [SerializeField] private TMP_Text textAssists;
        [SerializeField] private TMP_Text textPing;
        [SerializeField] private Graphic bg;

        public void Set(string name, int kills, int deaths, int assists, int ping, int index)
        {
            textName.text = name;
            textKills.text = kills.ToString();
            textDeaths.text = deaths.ToString();
            textAssists.text = assists.ToString();
            textPing.text = ping.ToString();

            bool even = index % 2 == 0;
            bg.SetAlpha(even ? 0.8f : 0.6f);
        }
    }
}

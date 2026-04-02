using ifp.arena.shared.Models;
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
        [SerializeField] private TMP_Text textHeadshotRatio;
        [SerializeField] private Graphic bg;

        public void Set(in PlayerStats stats, int index)
        {
            textName.text = stats.Name;
            textKills.text = stats.Kills.ToString();
            textDeaths.text = stats.Deaths.ToString();
            textAssists.text = stats.Assists.ToString();
            textPing.text = stats.Ping.ToString();

            SetHeadshotRatio(stats);

            bool even = index % 2 == 0;
            bg.SetAlpha(even ? 0.8f : 0.6f);
        }

        void SetHeadshotRatio(PlayerStats stats)
        {
            if (stats.Kills <= 0)
            {
                textHeadshotRatio.text = "-";
                return;
            }

            float ratio = stats.Headshots / stats.Kills;

            textHeadshotRatio.text = (ratio * 100f).ToString("0");
        }
    }
}

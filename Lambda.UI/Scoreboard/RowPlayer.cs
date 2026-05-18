using Lambda.Shared.Models;
using Lambda.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Lambda.UI.scoreboard
{
    public class RowPlayer : MonoBehaviour
    {
        [SerializeField] private TMP_Text textName;
        [SerializeField] private Image iconRouble;
        [SerializeField] private TMP_Text textMoney;
        [SerializeField] private TMP_Text textKills;
        [SerializeField] private TMP_Text textDeaths;
        [SerializeField] private TMP_Text textAssists;
        [SerializeField] private TMP_Text textPing;
        [SerializeField] private TMP_Text textHeadshotRatio;
        [SerializeField] private TMP_Text textDamage;
        [SerializeField] private Graphic bg;
        [SerializeField] private CanvasGroup canvasGroup;

        Faction _lastFaction;

        public void Set(PlayerContextInfo stats, bool isTeammate, int index)
        {
            if (_lastFaction != stats.Faction)
            {
                DecideIfToShowStats(stats.Faction);
            }

            textName.text = stats.Name;

            iconRouble.gameObject.SetActive(isTeammate);
            textMoney.text = isTeammate ? MoneyFormat.FormatMoney(stats.Money) : " ";

            textKills.text = stats.Kills.ToString();
            textDeaths.text = stats.Deaths.ToString();
            textAssists.text = stats.Assists.ToString();
            textPing.text = stats.Ping.ToString();

            SetHeadshotRatio(stats);

            textDamage.text = stats.Damage.ToString();

            canvasGroup.alpha = stats.IsAlive ? 1f : 0.5f;

            bool even = index % 2 == 0;
            bg.SetAlpha(even ? 0.8f : 0.6f);

            _lastFaction = stats.Faction;
        }

        // not triggering for some reason rn chat
        void DecideIfToShowStats(Faction faction)
        {
            var newAlpha = faction is not Faction.Spectator ? 1f : 0f;
            textMoney.SetAlpha(newAlpha);
            textKills.SetAlpha(newAlpha);
            textDeaths.SetAlpha(newAlpha);
            textAssists.SetAlpha(newAlpha);
            textPing.SetAlpha(newAlpha);
            textHeadshotRatio.SetAlpha(newAlpha);
            textDamage.SetAlpha(newAlpha);
        }

        void SetHeadshotRatio(PlayerContextInfo stats)
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

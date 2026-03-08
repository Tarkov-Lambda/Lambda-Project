using arena.ui.Scoreboard;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace arena.ui
{
    public class TeamBoard : MonoBehaviour
    {
        [SerializeField] private TMP_Text textTeamScore;

        [SerializeField] private RowPlayer prefabRowPlayer;
        [SerializeField] private RectTransform containerPlayers;

        [SerializeField] private Graphic[] coloredGraphicsKeepAlpha;
    }
}

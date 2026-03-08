using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace arena.ui.Scoreboard
{
    public class RowPlayer : MonoBehaviour
    {
        [SerializeField] private TMP_Text textName;
        [SerializeField] private TMP_Text textKills;
        [SerializeField] private TMP_Text textDeaths;
        [SerializeField] private TMP_Text textAssists;
        [SerializeField] private TMP_Text textPing;
    }
}

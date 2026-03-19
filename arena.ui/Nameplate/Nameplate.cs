using arena.ui;
using ifp.arena.shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ifp.arena.ui.Nameplate
{
    public class Nameplate : MonoBehaviour
    {
        [SerializeField] private TMP_Text textName;
        [SerializeField] private Graphic[] coloredGraphic;
        [SerializeField] private FactionColors factionColors;

        public void Set(string name, Faction faction)
        {
            textName.text = name;
            foreach (var graphic in coloredGraphic)
            {
                graphic.color = factionColors.Get(faction);
            }
        }


    }
}

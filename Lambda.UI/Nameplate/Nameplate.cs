using Lambda.UI;
using Lambda.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Lambda.UI.Nameplate
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

        public void SetTextAlpha(float alpha)
        {
            textName.alpha = alpha;
        }

        public void SetGraphicsAlpha(float alpha)
        {
            foreach (var graphic in coloredGraphic)
            {
                graphic.SetAlpha(alpha);
            }
        }
    }
}

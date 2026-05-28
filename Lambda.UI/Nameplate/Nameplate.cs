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

        private string _cachedName;
        private Faction _cachedFaction;

        public void Set(string name, Faction faction)
        {
            if (_cachedName != name || _cachedFaction != faction)
            {
                textName.text = name;
                _cachedName = name;

                Color newColor = factionColors.Get(faction);
                foreach (var graphic in coloredGraphic)
                    if (graphic.color != newColor) graphic.color = newColor;
            }
        }

        public void SetTextAlpha(float alpha)
        {
            textName.canvasRenderer.SetAlpha(alpha);
        }

        public void SetGraphicsAlpha(float alpha)
        {
            foreach (var graphic in coloredGraphic)
            {
                graphic.canvasRenderer.SetAlpha(alpha);
            }
        }
    }
}

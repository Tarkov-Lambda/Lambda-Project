using ifp.arena.shared;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace arena.ui
{
    public class FactionSelection : MonoBehaviour
    {
        [SerializeField] private Button buttonT;
        [SerializeField] private Button buttonCT;
        [SerializeField] private Button buttonSpectator;
        [SerializeField] private FactionColors factionColors;

        public event Action<Faction> OnFactionSelected;

        private void Start()
        {
            buttonT.onClick.AddListener(() => OnFactionSelected?.Invoke(Faction.T));
            buttonCT.onClick.AddListener(() => OnFactionSelected?.Invoke(Faction.CT));
            buttonSpectator.onClick.AddListener(() => OnFactionSelected?.Invoke(Faction.Spectator));

            buttonT.GetComponent<ColoredGraphics>().Set(factionColors.Get(Faction.T));
            buttonCT.GetComponent<ColoredGraphics>().Set(factionColors.Get(Faction.CT));
            buttonSpectator.GetComponent<ColoredGraphics>().Set(factionColors.Get(Faction.Spectator));
        }
    }
}

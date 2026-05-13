using Lambda.Shared;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace Lambda.UI
{
    public class FactionSelection : MonoBehaviour
    {
        [SerializeField] private Button buttonT;
        [SerializeField] private Button buttonCT;
        [SerializeField] private Button buttonSpectator;
        [SerializeField] private Button buttonCancel;
        [SerializeField] private FactionColors factionColors;

        public event Action<Faction> OnFactionSelected;
        public event Action OnCancelClicked;

        private void Start()
        {
            buttonT.onClick.AddListener(() => OnFactionSelected?.Invoke(Faction.T));
            buttonCT.onClick.AddListener(() => OnFactionSelected?.Invoke(Faction.CT));
            buttonSpectator.onClick.AddListener(() => OnFactionSelected?.Invoke(Faction.Spectator));
            buttonCancel.onClick.AddListener(() => OnCancelClicked?.Invoke());

            buttonT.SetColoredGraphicsColor(factionColors.Get(Faction.T));
            buttonCT.SetColoredGraphicsColor(factionColors.Get(Faction.CT));
            buttonSpectator.SetColoredGraphicsColor(factionColors.Get(Faction.Spectator));
        }
    }
}

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

        public event Action<Faction> OnFactionSelected;

        private void Start()
        {
            buttonT.onClick.AddListener(() => OnFactionSelected?.Invoke(Faction.T));
            buttonCT.onClick.AddListener(() => OnFactionSelected?.Invoke(Faction.CT));
        }
    }
}

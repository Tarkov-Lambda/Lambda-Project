using ifp.arena.shared.Models;
using TMPro;
using UnityEngine;

namespace arena.ui
{
    public class DeathInfo : MonoBehaviour
    {
        [SerializeField] CanvasGroup frame;

        [SerializeField] private TMP_Text textKillerName;
        [SerializeField] private FactionColors factionColors;

        float t;

        void Awake()
        {
            frame.alpha = 0f;
        }

        public void Pop(PlayerStats killer)
        {
            textKillerName.text = killer.Name;
            textKillerName.color = factionColors.Get(killer.Faction);

            frame.alpha = 1f;
            t = 0f;
        }

        void Update()
        {
            if (t < 1f)
            {
                t += Time.deltaTime * 0.2f;
                t = Mathf.Clamp01(t);
                frame.alpha = EasingFunctions.OutCirc(1f - t);
            }
        }
    }
}

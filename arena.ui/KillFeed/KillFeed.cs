using ifp.arena.shared;
using ifp.arena.shared.Models;
using UnityEngine;

namespace arena.ui.killfeed
{
    public class KillFeed : MonoBehaviour
    {
        [SerializeField] private FactionColors factionColors;
        [SerializeField] private KillNotification prefabNotification;

        RectTransform container => transform as RectTransform;

        public void Add(PlayerStats left, PlayerStats right, Sprite weapon, bool isHeadshot)
        {
            KillNotification notif = Instantiate(prefabNotification, container);
            notif.Set(left.Name, factionColors.Get(left.Faction), right.Name, factionColors.Get(right.Faction), weapon, isHeadshot);
            (notif.transform as RectTransform).anchorMin = new Vector2(1f, 1f);
            (notif.transform as RectTransform).anchorMax = new Vector2(1f, 1f);
            (notif.transform as RectTransform).pivot = new Vector2(1f, 1f);
            (notif.transform as RectTransform).anchoredPosition = new Vector2(0f, 0f);
        }
    }
}

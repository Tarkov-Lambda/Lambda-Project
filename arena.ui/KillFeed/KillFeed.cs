using ifp.arena.shared.Models;
using System.Collections.Generic;
using UnityEngine;

namespace arena.ui.killfeed
{
    public class KillFeed : MonoBehaviour
    {
        [SerializeField] private FactionColors factionColors;
        [SerializeField] private KillNotification prefabNotification;

        [SerializeField] private float killShowTime = 2f;
        [SerializeField] private float spacing = 5f;

        RectTransform container => transform as RectTransform;

        Queue<KillNotification> currentlyShowing = new Queue<KillNotification>();

        Stack<KillNotification> pool = new Stack<KillNotification>();

        void Awake()
        {
            // clear editor placeholders
            foreach (Transform item in container)
            {
                Destroy(item.gameObject);
            }
        }

        public void Add(PlayerStats left, PlayerStats right, Sprite weapon, bool isHeadshot)
        {
            KillNotification notif = SpawnOrGetFromPool();
            notif.gameObject.SetActive(true);
            notif.rectTransform.anchorMin = new Vector2(1f, 1f);
            notif.rectTransform.anchorMax = new Vector2(1f, 1f);
            notif.rectTransform.pivot = new Vector2(1f, 1f);

            notif.rectTransform.anchoredPosition = new Vector2(0, 0f);

            notif.Set(left.Name, factionColors.Get(left.Faction), right.Name, factionColors.Get(right.Faction), weapon, isHeadshot);

            foreach (var existingNotif in currentlyShowing)
            {
                existingNotif.rectTransform.anchoredPosition -= new Vector2(0, notif.rectTransform.sizeDelta.y + spacing);
            }

            currentlyShowing.Enqueue(notif);
        }

        void Update()
        {
            if (currentlyShowing.Count > 0 && currentlyShowing.Peek().TimeShowing > killShowTime)
                ReturnToPool(currentlyShowing.Dequeue());
        }

        KillNotification SpawnOrGetFromPool()
        {
            if (pool.Count > 0)
            {
                return pool.Pop();
            }
            else
            {
                return Instantiate(prefabNotification, container);
            }
        }

        void ReturnToPool(KillNotification notif)
        {
            notif.gameObject.SetActive(false);
            pool.Push(notif);
        }
    }
}

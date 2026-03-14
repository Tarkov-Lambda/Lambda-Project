using ifp.arena.shared;
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

        List<KillNotification> currentlyShowing = new List<KillNotification>();

        Stack<KillNotification> pool = new Stack<KillNotification>();

        void Awake()
        {
            // clear editor placeholders
            foreach (Transform item in container)
            {
                Destroy(item.gameObject);
            }
        }

        public void Add(string killerName, Faction killerFaction, string victimName, Faction victimFaction, Sprite weapon, bool isHeadshot)
        {
            KillNotification notif = SpawnOrGetFromPool();
            notif.gameObject.SetActive(true);
            notif.rectTransform.anchorMin = new Vector2(1f, 1f);
            notif.rectTransform.anchorMax = new Vector2(1f, 1f);
            notif.rectTransform.pivot = new Vector2(1f, 1f);

            notif.rectTransform.anchoredPosition = new Vector2(0, notif.rectTransform.sizeDelta.y + spacing);

            notif.Set(killerName, factionColors.Get(killerFaction), victimName, factionColors.Get(victimFaction), weapon, isHeadshot);
            notif.SetAlpha(1f);

            currentlyShowing.Add(notif);
        }

        void Update()
        {
            float offsetY = 0;
            for (int i = currentlyShowing.Count - 1; i >= 0; i--)
            {
                var notif = currentlyShowing[i];
                Vector2 targetPos = new Vector2(0, offsetY);
                notif.rectTransform.anchoredPosition = Vector2.Lerp(notif.rectTransform.anchoredPosition, targetPos, Time.deltaTime * 30f);
                offsetY -= notif.rectTransform.sizeDelta.y + spacing;

                float normalizedLifetime = ((Time.time - notif.ActivationTimeStamp) / killShowTime);
                float targetAlpha = Mathf.Lerp(3f, 0f, normalizedLifetime);
                notif.SetAlpha(targetAlpha);
            }

            if (currentlyShowing.Count > 0 && currentlyShowing[0].ActivationTimeStamp < Time.time - killShowTime)
            {
                ReturnToPool(currentlyShowing[0]);
                currentlyShowing.RemoveAt(0);
            }
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

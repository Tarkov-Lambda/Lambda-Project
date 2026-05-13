using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Lambda.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public class KillNotification : MonoBehaviour
    {
        public RectTransform rectTransform => transform as RectTransform;
        private CanvasGroup canvasGroup;

        [SerializeField] private TMP_Text leftText;
        [SerializeField] private Graphic leftColoredGraphic;

        [SerializeField] private TMP_Text rightText;
        [SerializeField] private Graphic rightColoredGraphic;

        [SerializeField] private RectTransform containerMiddle;
        [SerializeField] private Image imageWeapon;
        [SerializeField] private Image imageHeadshot;

        public float ActivationTimeStamp { get; private set; }

        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        private void OnEnable()
        {
            ActivationTimeStamp = Time.time;
        }

        public void Set(string left, Color colorLeft, string right, Color colorRight, bool isHeadshot)
        {
            leftText.text = left;
            leftColoredGraphic.color = colorLeft;
            rightText.text = right;
            rightColoredGraphic.color = colorRight;

            imageHeadshot.gameObject.SetActive(isHeadshot);

            SetWeaponSprite(null);
        }

        public void SetWeaponSprite(Sprite sprite)
        {
            imageWeapon.sprite = sprite;
            LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
        }

        public void SetAlpha(float alpha)
        {
            canvasGroup.alpha = alpha;
        }
    }
}

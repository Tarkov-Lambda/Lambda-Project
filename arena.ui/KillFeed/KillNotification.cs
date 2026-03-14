using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace arena.ui
{
    public class KillNotification : MonoBehaviour
    {
        public RectTransform rectTransform => transform as RectTransform;

        [SerializeField] private TMP_Text leftText;
        [SerializeField] private Graphic leftColoredGraphic;

        [SerializeField] private TMP_Text rightText;
        [SerializeField] private Graphic rightColoredGraphic;

        [SerializeField] private RectTransform containerMiddle;
        [SerializeField] private Image imageWeapon;
        [SerializeField] private Image imageHeadshot;

        public float ActivationTimeStamp { get; private set; }

        private void OnEnable()
        {
            ActivationTimeStamp = Time.time;
        }

        public void Set(string left, Color colorLeft, string right, Color colorRight, Sprite weapon, bool isHeadshot)
        {
            leftText.text = left;
            leftColoredGraphic.color = colorLeft;
            rightText.text = right;
            rightColoredGraphic.color = colorRight;

            imageWeapon.sprite = weapon;

            imageHeadshot.gameObject.SetActive(isHeadshot);

            LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
        }
    }
}

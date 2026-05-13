using Lambda.Shared;
using Lambda.Shared.Models;
using Lambda.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Lambda.UI
{
    public class ShopItemButton : MonoBehaviour
    {
        [SerializeField] private TMP_Text textName;
        [SerializeField] private TMP_Text textSubtext;
        [SerializeField] private TMP_Text textPrice;
        [SerializeField] private Image icon;

        [SerializeField] private Button mainButton;

        public event Action OnClick;

        void OnEnable()
        {
            mainButton.onClick.AddListener(() => OnClick?.Invoke());
        }

        void OnDisable()
        {
            mainButton.onClick.RemoveAllListeners();
        }

        public void Set(ShopItem shopItem, IItemInfoProvider itemInfoProvider)
        {
            textName.text = itemInfoProvider.ShortName(shopItem.bsgId);
            textSubtext.text = string.IsNullOrEmpty(shopItem.ammoId) ? string.Empty : itemInfoProvider.ShortName(shopItem.ammoId);
            textPrice.text = MoneyFormat.FormatMoney(shopItem.price);

            itemInfoProvider.RequestIcon(shopItem.bsgId, SetIconSprite);
        }

        void SetIconSprite(Sprite sprite)
        {
            icon.sprite = sprite;

            if (sprite == null) 
                return;

            RectTransform parentRect = icon.rectTransform.parent as RectTransform;

            Vector2 parentSize = parentRect.rect.size;
            Vector2 spriteSize = sprite.rect.size;

            float scale = Mathf.Min(1f, parentSize.x / spriteSize.x, parentSize.y / spriteSize.y);

            icon.rectTransform.sizeDelta = spriteSize * scale;
        }

        public void SetInteractable(bool interactable)
        {
            mainButton.interactable = interactable;
        }
    }
}

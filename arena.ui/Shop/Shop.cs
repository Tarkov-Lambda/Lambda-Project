using ifp.arena.shared;
using ifp.arena.ui;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace arena.ui
{
    public class Shop : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;

        [SerializeField] private TMP_Text textTimer;
        [SerializeField] private TMP_Text textMoney;
        [SerializeField] private RectTransform containerCategories;

        [SerializeField] private ShopCategory prefabShopCategory;
        [SerializeField] private ShopItemButton prefabShopItem;

        Dictionary<ShopItem, ShopItemButton> assortment;

        public void SetAssortment(List<BuyCategory> shelves, IItemInfoProvider itemInfoProvider, Action<ShopItem> onRequest)
        {
            foreach (Transform child in containerCategories)
            {
                Destroy(child.gameObject);
            }

            assortment = new Dictionary<ShopItem, ShopItemButton>();
            foreach (var shelf in shelves)
            {
                ShopCategory newShelf = Instantiate(prefabShopCategory, containerCategories).GetComponent<ShopCategory>();

                foreach (Transform child in newShelf.container)
                {
                    Destroy(child.gameObject);
                }

                newShelf.label = shelf.name;
                foreach (var product in shelf.items)
                {
                    ShopItemButton shopItemButton = Instantiate(prefabShopItem.gameObject, newShelf.container).GetComponent<ShopItemButton>();
                    shopItemButton.Set(product, itemInfoProvider);

                    shopItemButton.OnClick += () => onRequest?.Invoke(product);

                    assortment.Add(product, shopItemButton);
                }
            }
        }

        public void SetFaction(Faction faction)
        {
            foreach (var kvp in assortment)
            {
                bool available = kvp.Key.faction == faction || kvp.Key.faction == Faction.None;

                kvp.Value.gameObject.SetActive(available);
            }
        }

        public void SetInteractable(bool interactable)
        {
            canvasGroup.interactable = interactable;
        }

        public void SetCurrentMoneyBalance(int money)
        {
            foreach (var kvp in assortment)
            {
                kvp.Value.SetInteractable(kvp.Key.price <= money);
            }

            textMoney.text = MoneyFormat.FormatMoney(money);
        }
    }
}

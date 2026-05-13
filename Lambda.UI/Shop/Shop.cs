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
    public class Shop : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;

        [SerializeField] private TMP_Text textTimer;
        [SerializeField] private TMP_Text textMoney;
        [SerializeField] private RectTransform containerCategories;

        [SerializeField] private ShopCategory prefabShopCategory;
        [SerializeField] private ShopCategory prefabShopCategoryVerticalLayout;
        [SerializeField] private ShopItemButton prefabShopItem;

        Dictionary<ShopItem, ShopItemButton> assortment;

        HorizontalLayoutGroup currentRow;

        public void SetAssortment(List<BuyCategory> shelves, IItemInfoProvider itemInfoProvider, Action<ShopItem> onRequest)
        {
            foreach (Transform child in containerCategories) // full reset
            {
                Destroy(child.gameObject);
            }

            assortment = new Dictionary<ShopItem, ShopItemButton>();
            foreach (var shelf in shelves)
            {
                if (shelf.verticalLayout)
                {
                    bool needNewRow = currentRow == null || currentRow.transform.childCount >= 3;
                    if (needNewRow)
                    {
                        currentRow = new GameObject("HorizontalLayoutGroup")
                            .AddComponent<HorizontalLayoutGroup>();
                        currentRow.transform.SetParent(containerCategories);
                        currentRow.transform.localScale = Vector3.one;
                        currentRow.childControlWidth = true;
                    }
                }
                else
                {
                    currentRow = null; // break pairing when a non-vertical category appears
                }

                ShopCategory newShelf = Instantiate(
                    shelf.verticalLayout ? prefabShopCategoryVerticalLayout : prefabShopCategory,
                    shelf.verticalLayout ? currentRow.transform : containerCategories);

                foreach (Transform child in newShelf.container) // clean up placeholders left form editor
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

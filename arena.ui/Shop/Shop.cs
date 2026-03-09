using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace arena.ui
{
    public class Shop : MonoBehaviour
    {
        [SerializeField] private TMP_Text textTimer;
        [SerializeField] private RectTransform containerCategories;

        [SerializeField] private ShopCategory prefabShopCategory;
        [SerializeField] private ShopItem prefabShopItem;
    }
}

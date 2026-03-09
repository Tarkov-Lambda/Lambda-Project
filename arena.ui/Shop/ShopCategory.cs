using TMPro;
using UnityEngine;

namespace arena.ui
{
    public class ShopCategory : MonoBehaviour
    {
        [SerializeField] private TMP_Text textLabel;
        [SerializeField] private RectTransform containerShopItems;

        public string label
        {
            get => textLabel.text;
            set => textLabel.text = value;
        }

        public RectTransform container => containerShopItems;
    }
}

using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace arena.ui
{
    public class ShopItem : MonoBehaviour
    {
        [SerializeField] private TMP_Text textName;
        [SerializeField] private TMP_Text textSubtext;
        [SerializeField] private TMP_Text textPrice;
        [SerializeField] private Image icon;
    }
}

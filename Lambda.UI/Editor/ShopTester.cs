#if UNITY_EDITOR
using Lambda.UI;
using Lambda.Shared;
using Lambda.Shared.Models;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ShopTester : MonoBehaviour
{
    [SerializeField] private string pathJsonAssortment;

    [SerializeField] private Sprite dummySprite;
    [SerializeField] private Faction faction;

    public List<BuyCategory> buyCategories;

    Shop instance;


    void BuyRequest(ShopItem shopItem)
    {
    }

    void Update()
    {
        if (instance == null)
        {
            instance = FindFirstObjectByType<Shop>();

            if (instance != null)
            {

                string json = System.IO.File.ReadAllText(pathJsonAssortment);
                buyCategories = JsonConvert.DeserializeObject<List<BuyCategory>>(json);
                foreach (var category in buyCategories)
                {
                    List<ShopItem> filteredItems = new List<ShopItem>();
                    foreach (var item in category.items.ToList())
                    {
                        if (item.faction != faction && item.faction != Faction.None)
                            category.items.Remove(item);
                    }
                }

                instance.SetAssortment(buyCategories, new DummyItemInfoProvider(dummySprite), BuyRequest);
            }
        }
    }
}

public class DummyItemInfoProvider : IItemInfoProvider
{
    readonly Sprite dummySprite;

    public DummyItemInfoProvider(Sprite dummySprite)
    {
        this.dummySprite = dummySprite;
    }

    public string FullName(string bsgId) => bsgId;

    public void RequestIcon(string bsgId, Action<Sprite> callback)
    {
        callback?.Invoke(dummySprite);
    }

    public string ShortName(string bsgId) => bsgId;
}
#endif
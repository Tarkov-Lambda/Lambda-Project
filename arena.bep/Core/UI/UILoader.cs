using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using EFT.UI;
using HarmonyLib;
using ifp.arena.bep.Core.AssetBundleHandling;
using ifp.arena.bep.Core.Economy;
using ifp.arena.bep.Core.Gamemode;
using ifp.arena.bep.networking;
using ifp.arena.bep.Patches.Tarkov;
using ifp.arena.bep.Patches.Tarkov.UI;
using ifp.arena.shared;
using System;
using UnityEngine;

namespace ifp.arena.bep.Core.UI
{
    public class UILoader : Singleton<UILoader>, IDisposable
    {
        GameObject mainUI;
        GameObject shopUI;

        public UILoader()
        {
            Patch_CommonUI_Awake.OnAwake += LoadUI;

            if (Singleton<CommonUI>.Instantiated)
                LoadUI(Singleton<CommonUI>.Instance);
        }

        async void LoadUI(CommonUI commonUI)
        {
            AssetBundle uibundle = await Singleton<AssetBundleHandler>.Instance.LoadAssetBundle("arenaui");

            foreach (var item in uibundle.GetAllAssetNames())
            {
                H.Log("in arena UI bundle found " + item);
            }

            GameObject prefabMatchUI = uibundle.LoadAsset<GameObject>("Packages/com.ifp.arena.ui/ArenaMatchUI.prefab");

            mainUI = GameObject.Instantiate(prefabMatchUI, commonUI.EftBattleUIScreen.transform);

            GameObject prefabShopUI = uibundle.LoadAsset<GameObject>("Packages/com.ifp.arena.ui/Shop/Shop.prefab");

            ItemsPanel itemsPanel = AccessTools.Field(typeof(InventoryScreen), "_itemsPanel").GetValue(Singleton<CommonUI>.Instance.InventoryScreen) as ItemsPanel;
            Transform shopParent = (AccessTools.Field(typeof(ItemsPanel), "_simpleStashPanel").GetValue(itemsPanel) as SimpleStashPanel).transform.parent;

            shopUI = GameObject.Instantiate(prefabShopUI, shopParent);
            RectTransform shopRectTransform = shopUI.transform as RectTransform;
            shopRectTransform.anchorMin = new Vector2(0, 0);
            shopRectTransform.anchorMax = new Vector2(1, 1);
            shopRectTransform.offsetMin = new Vector2(0, 0);
            shopRectTransform.offsetMax = new Vector2(0, 0);
        }

        public void Dispose()
        {
            Patch_CommonUI_Awake.OnAwake -= LoadUI;

            if (mainUI != null)
                GameObject.Destroy(mainUI);

            if (shopUI != null)
                GameObject.Destroy(shopUI);
        }
    }
}


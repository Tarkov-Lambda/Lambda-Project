using arena.ui;
using Comfort.Common;
using EFT.UI;
using HarmonyLib;
using ifp.arena.bep.Core.AssetBundleHandling;
using ifp.arena.bep.Core.Gamemode;
using ifp.arena.bep.Patches.Tarkov.UI;
using System;
using UnityEngine;

namespace ifp.arena.bep.Core.UI
{
    public class UIManager : Singleton<UIManager>, IDisposable
    {
        ArenaMatchUI matchUIController;

        Shop shop;

        public UIManager()
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

            matchUIController = GameObject.Instantiate(prefabMatchUI, commonUI.EftBattleUIScreen.transform).GetComponent<ArenaMatchUI>();

            //commonUI.EftBattleUIScreen.gameObject.SetActive(true);

            GameObject prefabShopUI = uibundle.LoadAsset<GameObject>("Packages/com.ifp.arena.ui/Shop/Shop.prefab");

            ItemsPanel itemsPanel = AccessTools.Field(typeof(InventoryScreen), "_itemsPanel").GetValue(Singleton<CommonUI>.Instance.InventoryScreen) as ItemsPanel;
            Transform shopParent = (AccessTools.Field(typeof(ItemsPanel), "_simpleStashPanel").GetValue(itemsPanel) as SimpleStashPanel).transform.parent;

            shop = GameObject.Instantiate(prefabShopUI, shopParent).GetComponent<Shop>();
            RectTransform shopRectTransform = shop.transform as RectTransform;
            shopRectTransform.anchorMin = new Vector2(0, 0);
            shopRectTransform.anchorMax = new Vector2(1, 1);
            shopRectTransform.offsetMin = new Vector2(0, 0);
            shopRectTransform.offsetMax = new Vector2(0, 0);

            AhhhhWire();
        }

        void AhhhhWire()
        {
            EventBus.OnEnter += OnMatchStateEnter;

        }

        void OnMatchStateEnter(GameTypes.MatchState matchState)
        {
            Refresh();
        }

        void Refresh()
        {
            int scoreCT = H.Session.factionWins[shared.Faction.CT];
            int scoreT = H.Session.factionWins[shared.Faction.T];

            matchUIController.TopBar.SetScores(scoreCT, scoreT);
        }

        public void Dispose()
        {
            Patch_CommonUI_Awake.OnAwake -= LoadUI;

            EventBus.OnEnter -= OnMatchStateEnter;

            if (matchUIController != null)
                GameObject.Destroy(matchUIController.gameObject);

            if (shop != null)
                GameObject.Destroy(shop.gameObject);

            Release(this);
        }
    }
}


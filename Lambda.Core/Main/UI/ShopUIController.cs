using Lambda.UI;
using Comfort.Common;
using Cysharp.Threading.Tasks;
using EFT.InventoryLogic;
using EFT.UI;
using HarmonyLib;
using Lambda.Core.Main.Economy;
using Lambda.Core.Main.Gamemode;
using Lambda.Core.Networking;
using Lambda.Core.Patches.Tarkov.UI;
using Lambda.Shared;
using System;
using System.Reflection;
using UnityEngine;

namespace Lambda.Core.Main.UI
{
    internal class ShopUIController : IDisposable
    {
        private static readonly FieldInfo Field_InventoryScreen__itemsPanel = AccessTools.Field(typeof(InventoryScreen), "_itemsPanel");
        private static readonly FieldInfo Field_ItemsPanel__simpleStashPanel = AccessTools.Field(typeof(ItemsPanel), "_simpleStashPanel");

        private readonly IItemInfoProvider itemInfoProvider;

        Shop shop;

        public ShopUIController(CommonUI commonUI, AssetBundle uiBundle, IItemInfoProvider itemInfoProvider)
        {
            this.itemInfoProvider = itemInfoProvider;

            GameObject prefabShopUI = uiBundle.LoadAsset<GameObject>("Packages/com.lambda.editor/Lambda.UI/Shop/Shop.prefab");

            ItemsPanel itemsPanel = Field_InventoryScreen__itemsPanel.GetValue(Singleton<CommonUI>.Instance.InventoryScreen) as ItemsPanel;
            Transform shopParent = (Field_ItemsPanel__simpleStashPanel.GetValue(itemsPanel) as SimpleStashPanel).transform.parent;

            shop = GameObject.Instantiate(prefabShopUI, shopParent).GetComponent<Shop>();

            Patch_ItemsTabController_Show.OnShow += OnInventoryScreenOpen;
            EventBus.OnSelfMoneyChanged += OnSelfMoneyChanged;
            H.OnGameStarted += SetInteractable;
            EventBus.OnEnter += OnMatchStateEnter;
            EventBus.OnExit += OnMatchStateExit;

            shop.SetAssortment(BuyMenuSelection.buyCategories, itemInfoProvider, Purchasing.BuyItem);
        }

        public void Dispose()
        {
            Patch_ItemsTabController_Show.OnShow -= OnInventoryScreenOpen;
            EventBus.OnSelfMoneyChanged -= OnSelfMoneyChanged;
            H.OnGameStarted -= SetInteractable;
            EventBus.OnEnter -= OnMatchStateEnter;

            if (shop != null)
                GameObject.Destroy(shop.gameObject);
        }

        private void OnMatchStateEnter(MatchState state)
        {
            shop.SetFaction(H.MainPlayerScore.Faction);
            SetInteractable();
        }

        private async void OnMatchStateExit(MatchState state)
        {
            if (H.Gamemode is IGMBuyable buyableGamemode)
            {
                if (state is MatchState.RoundPrepare)
                {
                    await UniTask.WaitForSeconds(buyableGamemode.TimeInActivePhaseToBuy + 1);

                    SetInteractable();
                }
            }
        }

        void SetInteractable()
        {
            shop?.SetInteractable(H.MainPlayerScore.CanBuy());
            shop?.SetCurrentMoneyBalance(H.MainPlayerScore.Money);
        }

        void OnSelfMoneyChanged(int money)
        {
            shop?.SetCurrentMoneyBalance(money);
            SetInteractable();
        }

        void OnInventoryScreenOpen(CompoundItem containerLooting)
        {
            if (shop == null) return;

            shop.gameObject.SetActive(containerLooting == null);
        }


    }
}

using arena.ui;
using Comfort.Common;
using Cysharp.Threading.Tasks;
using EFT.InventoryLogic;
using EFT.UI;
using HarmonyLib;
using ifp.arena.bep.Core.Economy;
using ifp.arena.bep.Core.Gamemode;
using ifp.arena.bep.networking;
using ifp.arena.bep.Patches.Tarkov.UI;
using ifp.arena.shared;
using System;
using System.Reflection;
using UnityEngine;

namespace ifp.arena.bep.Core.UI
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

            GameObject prefabShopUI = uiBundle.LoadAsset<GameObject>("Packages/com.ifp.arena.ui/Shop/Shop.prefab");

            ItemsPanel itemsPanel = Field_InventoryScreen__itemsPanel.GetValue(Singleton<CommonUI>.Instance.InventoryScreen) as ItemsPanel;
            Transform shopParent = (Field_ItemsPanel__simpleStashPanel.GetValue(itemsPanel) as SimpleStashPanel).transform.parent;

            shop = GameObject.Instantiate(prefabShopUI, shopParent).GetComponent<Shop>();

            Patch_ItemsTabController_Show.OnShow += OnInventoryScreenOpen;
            EventBus.OnSelfMoneyChanged += OnSelfMoneyChanged;
            H.OnGameStarted += SetInteractable;
            EventBus.OnEnter += OnMatchStateEnter;
            EventBus.OnExit += OnMatchStateExit;
            PlayerKilledPacketHandler.AfterPacketApplied += OnPlayerKill;

            shop.SetAssortment(BuyMenuSelection.buyCategories, itemInfoProvider, Purchasing.BuyItem);
        }

        private void OnPlayerKill(PlayerKilledPacket packet)
        {
            shop.SetCurrentMoneyBalance(H.MainPlayerScore.Money);
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

        public void Dispose()
        {
            Patch_ItemsTabController_Show.OnShow -= OnInventoryScreenOpen;
            EventBus.OnSelfMoneyChanged -= OnSelfMoneyChanged;
            H.OnGameStarted -= SetInteractable;
            EventBus.OnEnter -= OnMatchStateEnter;
            PlayerKilledPacketHandler.AfterPacketApplied -= OnPlayerKill;

            if (shop != null)
                GameObject.Destroy(shop.gameObject);
        }
    }
}

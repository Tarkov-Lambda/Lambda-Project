using arena.ui;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using EFT.UI;
using HarmonyLib;
using ifp.arena.bep.Core.AssetBundleHandling;
using ifp.arena.bep.Core.Economy;
using ifp.arena.bep.Core.Gamemode;
using ifp.arena.bep.GameTypes;
using ifp.arena.bep.networking;
using ifp.arena.bep.Patches.Tarkov;
using ifp.arena.bep.Patches.Tarkov.UI;
using ifp.arena.shared;
using ifp.arena.shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ifp.arena.bep.Core.UI
{
    public class UIManager : Singleton<UIManager>, IDisposable
    {
        AssetBundle uibundle;

        ArenaMatchUI matchUIController;

        Shop shop;
        BSGItemInfoProvider itemInfoProvider;

        InventoryHotkeyListener inventoryHotkeyListener;

        public UIManager()
        {
            Patch_CommonUI_Awake.OnAwake += LoadUI;
            Patch_ItemsTabController_Show.OnShow += OnInventoryScreenOpen;
            Patch_Gameworld_OnGameStarted.OnGameStarted += AddInventoryHotkeyInterceptor;

            if (Singleton<CommonUI>.Instantiated)
                LoadUI(Singleton<CommonUI>.Instance);

            if (Singleton<GameWorld>.Instantiated)
                AddInventoryHotkeyInterceptor(Singleton<GameWorld>.Instance);
        }

        private void AddInventoryHotkeyInterceptor(GameWorld gameWorld)
        {
            inventoryHotkeyListener = gameWorld.MainPlayer.gameObject.AddComponent<InventoryHotkeyListener>();
            inventoryHotkeyListener.OnHoldBegin += () => matchUIController.ToggleScoreboard(true);
            inventoryHotkeyListener.OnHoldEnd += () => matchUIController.ToggleScoreboard(false);
        }

        async void LoadUI(CommonUI commonUI)
        {
            uibundle = AssetBundle.LoadFromFile(System.IO.Path.Combine(AssetBundleHandler.pathToBundlesDir, "arenaui"));

            foreach (var item in uibundle.GetAllAssetNames())
            {
                H.Log("in arena UI bundle found " + item);
            }

            GameObject prefabMatchUI = uibundle.LoadAsset<GameObject>("Packages/com.ifp.arena.ui/ArenaMatchUI.prefab");

            matchUIController = GameObject.Instantiate(prefabMatchUI, commonUI.EftBattleUIScreen.transform).GetComponent<ArenaMatchUI>();
            matchUIController.ToggleScoreboard(false);

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
            EventBus.OnPlayerKill += OnPlayerKill;

            H.Arena.OnUpdateTick += () => matchUIController.TopBar.SetTime(H.Arena.StateTimer);

        }

        void OnMatchStateEnter(GameTypes.MatchState matchState)
        {
            if (itemInfoProvider == null)
            {
                itemInfoProvider = new BSGItemInfoProvider();
                shop.SetAssortment(BuyMenu.buyCategories, itemInfoProvider, Purchasing.BuyItem);
            }

            shop.SetFaction(H.MainPlayerScore.faction);
            shop.SetInteractable(matchState == GameTypes.MatchState.RoundPrepare);

            Refresh();
        }

        void OnPlayerKill(PlayerKilledPacket killPacket)
        {
            Refresh();
        }

        void Refresh()
        {
            int scoreCT = H.Session.factionWins[Faction.CT];
            int scoreT = H.Session.factionWins[Faction.T];

            matchUIController.TopBar.SetScores(scoreCT, scoreT);

            matchUIController.Scoreboard.SetPlayers(GetAllPlayersStats(), H.Session.factionWins);

            shop.SetCurrentMoneyBalance(H.MainPlayerScore.money);
        }

        PlayerStats[] GetAllPlayersStats()
        {
            List<PlayerStats> playerStats = new List<PlayerStats>();
            foreach (var kvp in H.Scoreboard)
            {
                int id = kvp.Key;
                PlayerScore playerScore = kvp.Value;

                playerStats.Add(new PlayerStats
                {
                    Id = id,
                    Faction = playerScore.faction,
                    Name = playerScore.player.name,
                    Kills = playerScore.kills,
                    Deaths = playerScore.deaths,
                    Assists = playerScore.assists,
                    Ping = playerScore.ping 
                });
            }

            return playerStats.ToArray();
        }

        void OnInventoryScreenOpen(CompoundItem containerLooting)
        {
            if (shop == null) return;

            shop.gameObject.SetActive(containerLooting == null);
        }

        public void Dispose()
        {
            Patch_CommonUI_Awake.OnAwake -= LoadUI;
            Patch_ItemsTabController_Show.OnShow -= OnInventoryScreenOpen;
            Patch_Gameworld_OnGameStarted.OnGameStarted -= AddInventoryHotkeyInterceptor;

            EventBus.OnEnter -= OnMatchStateEnter;

            if (matchUIController != null)
                GameObject.Destroy(matchUIController.gameObject);

            if (shop != null)
                GameObject.Destroy(shop.gameObject);

            if (inventoryHotkeyListener != null)
                Component.Destroy(inventoryHotkeyListener);

            uibundle.Unload(unloadAllLoadedObjects: false);

            Release(this);
        }
    }
}


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
using ifp.arena.ui.Nameplate;
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

        NameplateRenderer nameplateRenderer;

        public Material MatteMaterial { get; private set; }

        public UIManager()
        {
            Patch_CommonUI_Awake.OnAwake += LoadUI;
            Patch_ItemsTabController_Show.OnShow += OnInventoryScreenOpen;
            Patch_Gameworld_OnGameStarted.OnGameStarted += AddInventoryHotkeyInterceptor;

            if (Singleton<CommonUI>.Instantiated)
                LoadUI(Singleton<CommonUI>.Instance);

            if (Singleton<GameWorld>.Instantiated)
                AddInventoryHotkeyInterceptor(Singleton<GameWorld>.Instance);

            EventBus.OnEnter += OnMatchStateEnter;
            EventBus.OnRoundActionEnd += OnRoundActionEnd;
            EventBus.OnPlayerKill += OnPlayerKill;

            EventBus.OnUpdate += UpdateTime;

            EventBus.OnFixedUpdate += SetInteractable;
            EventBus.OnSelfMoneyChanged += OnSelfMoneyChanged;
        }

        public void SetInteractable()
        {
            if (H.MainPlayerScore is null) return;
            shop?.SetInteractable(H.MainPlayerScore.CanBuy());
        }

        void OnSelfMoneyChanged(int money)
        {
            shop?.SetCurrentMoneyBalance(money);
        }

        private void AddInventoryHotkeyInterceptor(GameWorld gameWorld)
        {
            inventoryHotkeyListener = gameWorld.MainPlayer.gameObject.AddComponent<InventoryHotkeyListener>();
            inventoryHotkeyListener.OnHoldBegin += () => matchUIController.ToggleScoreboard(true);
            inventoryHotkeyListener.OnHoldEnd += () => matchUIController.ToggleScoreboard(false);
        }

        async void LoadUI(CommonUI commonUI)
        {
            uibundle = AssetBundle.LoadFromFile(System.IO.Path.Combine(MapAssetBundleHandler.pathToBundlesDir, "arenaui"));

            foreach (var item in uibundle.GetAllAssetNames())
            {
                D.Log("in arena UI bundle found " + item);
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

            nameplateRenderer = new GameObject("Nameplate Renderer", typeof(RectTransform), typeof(NameplateRenderer)).GetComponent<NameplateRenderer>();
            GameObject prefabNameplate = uibundle.LoadAsset<GameObject>("Packages/com.ifp.arena.ui/Nameplate/Nameplate.prefab");
            nameplateRenderer.Init(commonUI, prefabNameplate.GetComponent<Nameplate>());

            MatteMaterial = uibundle.LoadAsset<Material>("Packages/com.ifp.arena.ui/UIMatte.mat");
        }

        private void UpdateTime()
        {
            if (H.Arena is null) return;
            matchUIController.TopBar.SetTime(H.Arena.StateTimer);
        }

        void OnRoundActionEnd(RoundActionPhaseEnd data)
        {
            bool win = data.winner == H.MainPlayerScore.faction;
            string mainTitle = win ? "ROUND WON" : "ROUND LOST";

            string subTitle = $"{H.GetPlayer(data.mvpId).Profile.Nickname} awarded for {data.mvpReason}";

            matchUIController.PopupMatchEnd.Pop(win, mainTitle, subTitle);
        }

        void OnMatchStateEnter(GameTypes.MatchState matchState)
        {
            if (itemInfoProvider == null)
            {
                itemInfoProvider = new BSGItemInfoProvider();
                shop.SetAssortment(BuyMenu.buyCategories, itemInfoProvider, Purchasing.BuyItem);
            }

            shop.SetFaction(H.MainPlayerScore.faction);
            Refresh();
        }

        void OnPlayerKill(PlayerKilledPacket killPacket)
        {
            Plugin.Logger.LogInfo(killPacket);

            try
            {
                H.Scoreboard.TryGetValue(killPacket.killerId, out PlayerScore playerKiller);
                H.Scoreboard.TryGetValue(killPacket.victimId, out PlayerScore playerVictim);

                string leftName = playerKiller?.player.Profile.Nickname;
                string rightName = playerVictim?.player.Profile.Nickname;

                Faction leftFaction = playerKiller == null ? Faction.None : playerKiller.faction;
                Faction rightFaction = playerVictim == null ? Faction.None : playerVictim.faction;

                itemInfoProvider.RequestIcon(killPacket.weaponId, (weaponSprite) =>
                {
                    matchUIController.KillFeed.Add(
                        leftName, leftFaction,
                        rightName, rightFaction,
                        weaponSprite, killPacket.IsHeadshot);

                    matchUIController.DeathInfo.Pop(GetPlayerStats(playerKiller));
                });

                Refresh();
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError(ex);
            }
        }

        void Refresh()
        {
            int scoreCT = H.Session.factionWins[Faction.CT];
            int scoreT = H.Session.factionWins[Faction.T];

            matchUIController.TopBar.SetScores(scoreCT, scoreT);

            matchUIController.Scoreboard.SetPlayers(GetAllPlayersStats(), H.Session.factionWins, H.MainPlayerScore.faction);

            shop.SetCurrentMoneyBalance(H.MainPlayerScore.money);
        }

        PlayerStats[] GetAllPlayersStats()
        {
            List<PlayerStats> playerStats = new List<PlayerStats>();
            foreach (var kvp in H.Scoreboard)
            {
                int id = kvp.Key;
                PlayerScore playerScore = kvp.Value;

                playerStats.Add(GetPlayerStats(playerScore));
            }

            return playerStats.ToArray();
        }

        PlayerStats GetPlayerStats(PlayerScore playerScore)
        {
            return new PlayerStats
            {
                Id = playerScore.player.Id,
                Alive = playerScore.isAlive,
                Faction = playerScore.faction,

                Name = playerScore.player.Profile.Nickname,

                Money = playerScore.money,

                Kills = playerScore.kills,
                Deaths = playerScore.deaths,
                Assists = playerScore.assists,
                Ping = playerScore.ping,
                Headshots = playerScore.headshots,
                Damage = playerScore.damage
            };
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
            EventBus.OnRoundActionEnd -= OnRoundActionEnd;
            EventBus.OnPlayerKill -= OnPlayerKill;
            EventBus.OnUpdate -= UpdateTime;

            EventBus.OnFixedUpdate -= SetInteractable;
            EventBus.OnSelfMoneyChanged -= OnSelfMoneyChanged;

            if (matchUIController != null)
                GameObject.Destroy(matchUIController.gameObject);

            if (shop != null)
                GameObject.Destroy(shop.gameObject);

            if (nameplateRenderer != null)
                GameObject.Destroy(nameplateRenderer.gameObject);

            if (inventoryHotkeyListener != null)
                Component.Destroy(inventoryHotkeyListener);

            uibundle.Unload(unloadAllLoadedObjects: false);

            Release(this);
        }
    }
}


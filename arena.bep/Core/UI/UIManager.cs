using arena.ui;
using Comfort.Common;
using EFT;
using EFT.UI;
using EFT.UI.Screens;
using ifp.arena.bep.Core.AssetBundleHandling;
using ifp.arena.bep.Core.Gamemode;
using ifp.arena.bep.networking;
using ifp.arena.bep.Patches.Tarkov;
using ifp.arena.bep.Patches.Tarkov.UI;
using ifp.arena.bep.Patches.Tarkov.UI.QuickAccess;
using ifp.arena.bep.Patches.Tarkov.UI.WeaponBuilds;
using ifp.arena.shared.Models;
using ifp.arena.ui.Nameplate;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ifp.arena.bep.Core.UI;

public class UIManager : IDisposable
{
    AssetBundle uibundle;

    List<IDisposable> disposables = new();

    ArenaMatchUI matchUIController;

    InventoryHotkeyListener inventoryHotkeyListener;

    NameplateRenderer nameplateRenderer;

    FactionSelectionScreen factionSelectionScreen;

    public UIManager()
    {
        if (H.IsHeadless) return;

        Patch_CommonUI_Awake.OnAwake += LoadUI;
        Patch_Gameworld_OnGameStarted.OnGameStarted += AddInventoryHotkeyInterceptor;

        if (Singleton<CommonUI>.Instantiated)
            LoadUI(Singleton<CommonUI>.Instance);

        if (Singleton<GameWorld>.Instantiated) AddInventoryHotkeyInterceptor();

        EventBus.OnEnter += OnMatchStateEnter;
        EventBus.OnRoundActionEnd += OnRoundActionEnd;
        EventBus.OnPlayerKill += OnPlayerKill;

        EventBus.OnUpdate += UpdateTime;

        EventBus.OnSelfFactionChanged += OnSelfFactionChanged;
        EventBus.OnSelfRespawn += OnSelfRespawn;

        disposables.Add(new EFTCameraHook());
    }

    void OnSelfFactionChanged(Faction faction)
    {
        if (factionSelectionScreen.gameObject.activeSelf)
            factionSelectionScreen.Cancel();
    }

    private void AddInventoryHotkeyInterceptor()
    {
        inventoryHotkeyListener = H.MainPlayer.gameObject.AddComponent<InventoryHotkeyListener>();
        inventoryHotkeyListener.OnHoldBegin += () => matchUIController.Scoreboard.gameObject.SetActive(true);
        inventoryHotkeyListener.OnHoldEnd += () => matchUIController.Scoreboard.gameObject.SetActive(false);
    }

    async void LoadUI(CommonUI commonUI)
    {
        BSGItemInfoProvider itemInfoProvider = new BSGItemInfoProvider();

        uibundle = AssetBundle.LoadFromFile(System.IO.Path.Combine(MapAssetBundleHandler.pathToBundlesDir, "arenaui"));

        foreach (var item in uibundle.GetAllAssetNames())
        {
            D.Log("in arena UI bundle found " + item);
        }

        GameObject prefabMatchUI = uibundle.LoadAsset<GameObject>("Packages/com.ifp.arena.ui/ArenaMatchUI.prefab");

        matchUIController = GameObject.Instantiate(prefabMatchUI, commonUI.EftBattleUIScreen.transform).GetComponent<ArenaMatchUI>();
        matchUIController.Scoreboard.gameObject.SetActive(false);
        matchUIController.transform.SetAsFirstSibling();

        disposables.Add(new ShopUIController(commonUI, uibundle, itemInfoProvider));
        disposables.Add(new KillFeedController(matchUIController, itemInfoProvider));
        disposables.Add(new SpectatorController(matchUIController));
        disposables.Add(new EditBuildController(commonUI, uibundle));

        nameplateRenderer = new GameObject("Nameplate Renderer", typeof(RectTransform), typeof(NameplateRenderer)).GetComponent<NameplateRenderer>();
        GameObject prefabNameplate = uibundle.LoadAsset<GameObject>("Packages/com.ifp.arena.ui/Nameplate/Nameplate.prefab");
        nameplateRenderer.Init(commonUI, prefabNameplate.GetComponent<Nameplate>());

        GameObject prefabFactionSelection = uibundle.LoadAsset<GameObject>("Packages/com.ifp.arena.ui/FactionSelection/FactionSelection.prefab");
        factionSelectionScreen = GameObject.Instantiate(prefabFactionSelection, commonUI.transform.GetChild(0)).AddComponent<FactionSelectionScreen>();
        factionSelectionScreen.transform.SetSiblingIndex(1);
        EftScreenManager.Instance.RegisterScreen(FactionSelectionScreen.FAKETYPE, factionSelectionScreen);
        factionSelectionScreen.Close();

        //bruh
        PatchGroup_QuickAccessPanel_ModifyItemIcon.MatteMaterial = uibundle.LoadAsset<Material>("Packages/com.ifp.arena.ui/UIMatte.mat"); 



    }

    private void UpdateTime()
    {
        if (H.Arena is null) return;
        matchUIController.TopBar.SetTime(H.Arena.StateTimer);

#if DEBUG
        if (Input.GetKeyDown(KeyCode.P))
        {
            ShowFactionSelectionScreen();
        }
#endif
    }

    void ShowFactionSelectionScreen()
    {
        FactionSelectionScreen.FactionSelectionScreenController screenController = new(Singleton<FactionChangePacketHandler>.Instance.Send);
        screenController.ShowScreen(EScreenState.Temporary);
    }

    void OnRoundActionEnd(RoundActionPhaseEnd data)
    {
        bool win = data.winner == H.MainPlayerScore.Faction;
        string mainTitle = win ? "ROUND WON" : "ROUND LOST";

        string subTitle = "";

        if (H.GetPlayer(data.mvpId) != null && data.mvpReason != null)
        {
            subTitle = $"{H.GetPlayer(data.mvpId).Profile.Nickname} awarded for {data.mvpReason}";
        }

        matchUIController.PopupMatchEnd.Pop(win, mainTitle, subTitle);
    }

    void OnMatchStateEnter(MatchState matchState)
    {
        Refresh();
    }

    void OnPlayerKill(PlayerKilledPacket killPacket)
    {
        if (killPacket.victim == H.MainPlayer)
        {
            PlayerScore killerScore = H.GetPlayerScore(killPacket.killer);
            OnSelfDeath(killerScore.Score);
        }

        Refresh();
    }

    void OnSelfDeath(PlayerScoreInfo killer)
    {
        matchUIController.DeathInfo.Pop(killer);

        Singleton<CommonUI>.Instance.EftBattleUIScreen.UpdatePanelsVisibility(false);
    }

    void OnSelfRespawn()
    {
        Singleton<CommonUI>.Instance.EftBattleUIScreen.UpdatePanelsVisibility(true);
    }

    void Refresh()
    {
        int scoreCT = H.Session.factionWins[Faction.CT];
        int scoreT = H.Session.factionWins[Faction.T];

        matchUIController.TopBar.SetScores(scoreCT, scoreT);

        PlayerScoreInfo[] allPlayerStats = H.Scoreboard.Values.Select(p => p.Score).ToArray();

        PlayerScoreInfo[] teamT = allPlayerStats.Where(p => p.Faction == Faction.T).ToArray();
        PlayerScoreInfo[] teamCT = allPlayerStats.Where(p => p.Faction == Faction.CT).ToArray();

        matchUIController.TopBar.SetTeamStatuses(teamCT, teamT);

        matchUIController.Scoreboard.SetPlayers(allPlayerStats, H.Session.factionWins, H.MainPlayerScore.Faction);
    }


    public void Dispose()
    {
        Patch_CommonUI_Awake.OnAwake -= LoadUI;
        Patch_Gameworld_OnGameStarted.OnGameStarted -= AddInventoryHotkeyInterceptor;

        EventBus.OnEnter -= OnMatchStateEnter;
        EventBus.OnRoundActionEnd -= OnRoundActionEnd;
        EventBus.OnPlayerKill -= OnPlayerKill;
        EventBus.OnUpdate -= UpdateTime;

        EventBus.OnSelfFactionChanged -= OnSelfFactionChanged;
        EventBus.OnSelfRespawn -= OnSelfRespawn;

        foreach (var controller in disposables)
        {
            controller.Dispose();
        }
        disposables.Clear();

        if (factionSelectionScreen != null)
        {
            if (EftScreenManager.Instance.CurrentBaseScreenController is FactionSelectionScreen.FactionSelectionScreenController)
                EftScreenManager.Instance.CloseCurrentScreenForced();
            EftScreenManager.Instance.ReleaseScreen(FactionSelectionScreen.FAKETYPE, factionSelectionScreen);
            GameObject.Destroy(factionSelectionScreen.gameObject);
        }

        if (matchUIController != null)
            GameObject.Destroy(matchUIController.gameObject);

        if (nameplateRenderer != null)
            GameObject.Destroy(nameplateRenderer.gameObject);

        if (inventoryHotkeyListener != null)
            Component.Destroy(inventoryHotkeyListener);

        PatchGroup_QuickAccessPanel_ModifyItemIcon.MatteMaterial = null;

        uibundle.Unload(unloadAllLoadedObjects: false);
    }
}
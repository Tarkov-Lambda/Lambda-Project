using arena.ui;
using Comfort.Common;
using EFT.UI;
using EFT.UI.Screens;
using ifp.arena.bep.Core.AssetBundleHandling;
using ifp.arena.bep.Core.Gamemode;
using ifp.arena.bep.networking;
using ifp.arena.bep.Patches.Tarkov.UI;
using ifp.arena.bep.Patches.Tarkov.UI.QuickAccess;
using ifp.arena.shared.Models;
using ifp.arena.ui.Nameplate;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ifp.arena.bep.Core.UI;

public class UIManager : IDisposable
{
    AssetBundle uibundle;

    List<IDisposable> disposables = new();

    ArenaMatchUI matchUIController;

    FactionSelectionScreen factionSelectionScreen;

    public UIManager()
    {
        if (H.IsHeadless) return;

        Patch_CommonUI_Awake.OnAwake += LoadUI;
        if (Singleton<CommonUI>.Instantiated)
            LoadUI(Singleton<CommonUI>.Instance);


        EventBus.OnPlayerKill += OnPlayerKill;

        EventBus.OnUpdate += OnUpdate;

        EventBus.OnSelfFactionChanged += OnSelfFactionChanged;
        EventBus.OnSelfRespawn += OnSelfRespawn;

        disposables.Add(new EFTCameraHook());
    }

    void OnSelfFactionChanged(Faction faction)
    {
        if (factionSelectionScreen.gameObject.activeSelf)
            factionSelectionScreen.Cancel();
    }


    void LoadUI(CommonUI commonUI)
    {
        BSGItemInfoProvider itemInfoProvider = new BSGItemInfoProvider();

        uibundle = AssetBundle.LoadFromFile(System.IO.Path.Combine(MapAssetBundleHandler.pathToBundlesDir, "arenaui"));

        GameObject prefabMatchUI = uibundle.LoadAsset<GameObject>("Packages/com.ifp.arena.ui/ArenaMatchUI.prefab");
        matchUIController = GameObject.Instantiate(prefabMatchUI, commonUI.EftBattleUIScreen.transform).GetComponent<ArenaMatchUI>();
        matchUIController.transform.SetAsFirstSibling();

        disposables.Add(new ScoreboardController(matchUIController));
        disposables.Add(new TopBarController(matchUIController));
        disposables.Add(new ShopUIController(commonUI, uibundle, itemInfoProvider));
        disposables.Add(new KillFeedController(matchUIController, itemInfoProvider));
        disposables.Add(new MatchResultController(matchUIController));
        disposables.Add(new NameplateController(commonUI, uibundle));
        disposables.Add(new SpectatorController(matchUIController));
        disposables.Add(new EditBuildController(commonUI, uibundle));

        GameObject prefabFactionSelection = uibundle.LoadAsset<GameObject>("Packages/com.ifp.arena.ui/FactionSelection/FactionSelection.prefab");
        factionSelectionScreen = GameObject.Instantiate(prefabFactionSelection, commonUI.transform.GetChild(0)).AddComponent<FactionSelectionScreen>();
        factionSelectionScreen.transform.SetSiblingIndex(1);
        EftScreenManager.Instance.RegisterScreen(FactionSelectionScreen.FAKETYPE, factionSelectionScreen);
        factionSelectionScreen.Close();

        //bruh
        PatchGroup_QuickAccessPanel_ModifyItemIcon.MatteMaterial = uibundle.LoadAsset<Material>("Packages/com.ifp.arena.ui/UIMatte.mat"); 



    }

    private void OnUpdate()
    {
#if DEBUG
        if (Input.GetKeyDown(KeyCode.M))
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

    void OnPlayerKill(PlayerKilledPacket killPacket)
    {
        if (killPacket.victim == H.MainPlayer)
        {
            PlayerScore killerScore = H.GetPlayerScore(killPacket.killer);
            OnSelfDeath(killerScore.Score);
        }
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


    public void Dispose()
    {
        Patch_CommonUI_Awake.OnAwake -= LoadUI;

        EventBus.OnPlayerKill -= OnPlayerKill;
        EventBus.OnUpdate -= OnUpdate;

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

        PatchGroup_QuickAccessPanel_ModifyItemIcon.MatteMaterial = null;

        uibundle.Unload(unloadAllLoadedObjects: false);
    }
}
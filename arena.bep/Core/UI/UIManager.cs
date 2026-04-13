using arena.ui;
using Comfort.Common;
using EFT.UI;
using EFT.UI.Screens;
using ifp.arena.bep.Core.AssetBundleHandling;
using ifp.arena.bep.Core.Gamemode;
using ifp.arena.bep.networking;
using ifp.arena.bep.Patches.Tarkov.UI;
using ifp.arena.bep.Patches.Tarkov.UI.QuickAccess;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ifp.arena.bep.Core.UI;

public class UIManager : IDisposable
{
    AssetBundle uibundle;

    List<IDisposable> disposables = new();

    ArenaMatchUI matchUI;

    FactionSelectionScreen factionSelectionScreen;

    public UIManager()
    {
        if (H.IsHeadless) return;

        Patch_CommonUI_Awake.OnAwake += LoadUI;
        if (Singleton<CommonUI>.Instantiated)
            LoadUI(Singleton<CommonUI>.Instance);

        EventBus.OnUpdate += OnUpdate;

        EventBus.OnSelfFactionChanged += OnSelfFactionChanged;

        disposables.Add(new EFTCameraHook());
    }

    void LoadUI(CommonUI commonUI)
    {
        BSGItemInfoProvider itemInfoProvider = new BSGItemInfoProvider();

        uibundle = AssetBundle.LoadFromFile(System.IO.Path.Combine(MapAssetBundleHandler.pathToBundlesDir, "arenaui"));

        GameObject prefabMatchUI = uibundle.LoadAsset<GameObject>("Packages/com.ifp.arena.ui/ArenaMatchUI.prefab");
        matchUI = GameObject.Instantiate(prefabMatchUI, commonUI.EftBattleUIScreen.transform).GetComponent<ArenaMatchUI>();
        matchUI.transform.SetAsFirstSibling();

        disposables.Add(new ScoreboardController(matchUI.Scoreboard));
        disposables.Add(new TopBarController(matchUI.TopBar));
        disposables.Add(new KillFeedController(matchUI.KillFeed, itemInfoProvider));
        disposables.Add(new MatchResultController(matchUI.PopupMatchEnd));
        disposables.Add(new SpectatorController(matchUI.Spectator));

        disposables.Add(new ShopUIController(commonUI, uibundle, itemInfoProvider));
        disposables.Add(new NameplateController(commonUI, uibundle));
        disposables.Add(new EditBuildController(commonUI, uibundle));

        GameObject prefabFactionSelection = uibundle.LoadAsset<GameObject>("Packages/com.ifp.arena.ui/FactionSelection/FactionSelection.prefab");
        factionSelectionScreen = GameObject.Instantiate(prefabFactionSelection, commonUI.transform.GetChild(0)).AddComponent<FactionSelectionScreen>();
        factionSelectionScreen.transform.SetSiblingIndex(1);
        EftScreenManager.Instance.RegisterScreen(FactionSelectionScreen.FAKETYPE, factionSelectionScreen);
        factionSelectionScreen.Close();

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

    void OnSelfFactionChanged(Faction faction)
    {
        if (factionSelectionScreen.gameObject.activeSelf)
            factionSelectionScreen.Cancel();
    }

    public void Dispose()
    {
        Patch_CommonUI_Awake.OnAwake -= LoadUI;

        EventBus.OnUpdate -= OnUpdate;
        EventBus.OnSelfFactionChanged -= OnSelfFactionChanged;

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

        if (matchUI != null)
            GameObject.Destroy(matchUI.gameObject);

        PatchGroup_QuickAccessPanel_ModifyItemIcon.MatteMaterial = null;

        uibundle.Unload(unloadAllLoadedObjects: false);
    }
}
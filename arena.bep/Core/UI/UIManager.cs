using arena.ui;
using Comfort.Common;
using EFT.UI;
using ifp.arena.bep.Core.AssetBundleHandling;
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

    public UIManager()
    {
        if (H.IsHeadless) return;

        H.AfterApplicationLoaded += Initialize;

        // hot reload
        if (H.HasMainMenuLoaded()) Initialize();
    }

    public void Initialize()
    {
        Patch_CommonUI_Awake.OnAwake += LoadUI;
        if (Singleton<CommonUI>.Instantiated)
            LoadUI(Singleton<CommonUI>.Instance);

        disposables.Add(new EFTCameraHook());
    }

    public void Dispose()
    {
        H.AfterApplicationLoaded -= Initialize;
        Patch_CommonUI_Awake.OnAwake -= LoadUI;

        foreach (var controller in disposables)
        {
            controller.Dispose();
        }
        disposables.Clear();

        if (matchUI != null)
            GameObject.Destroy(matchUI.gameObject);

        PatchGroup_QuickAccessPanel_ModifyItemIcon.MatteMaterial = null;

        uibundle.Unload(unloadAllLoadedObjects: false);
    }

    void LoadUI(CommonUI commonUI)
    {
        Plugin.Logger.LogInfo("Loading UIManager");

        BSGItemInfoProvider itemInfoProvider = new BSGItemInfoProvider();

        uibundle = AssetBundle.LoadFromFile(System.IO.Path.Combine(MapAssetBundleHandler.pathToBundlesDir, "arenaui"));

        GameObject prefabMatchUI = uibundle.LoadAsset<GameObject>("Packages/com.ifp.arena.ui/ArenaMatchUI.prefab");
        matchUI = GameObject.Instantiate(prefabMatchUI, commonUI.EftBattleUIScreen.transform).GetComponent<ArenaMatchUI>();
        matchUI.transform.SetAsFirstSibling();

        try
        {
            disposables.Add(new ScoreboardController(matchUI.Scoreboard));
            disposables.Add(new TopBarController(matchUI.TopBar));
            disposables.Add(new KillFeedController(matchUI.KillFeed, itemInfoProvider));
            disposables.Add(new MatchResultController(matchUI.PopupMatchEnd));
            disposables.Add(new SelfDeathController(matchUI.DeathInfo));
            disposables.Add(new SpectatorController(matchUI.Spectator));

            disposables.Add(new ShopUIController(commonUI, uibundle, itemInfoProvider));
            disposables.Add(new NameplateController(commonUI, uibundle));
            disposables.Add(new EditBuildController(commonUI, uibundle));

            disposables.Add(new FactionSelectionController(commonUI, uibundle));
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError(e);
        }

        PatchGroup_QuickAccessPanel_ModifyItemIcon.MatteMaterial = uibundle.LoadAsset<Material>("Packages/com.ifp.arena.ui/UIMatte.mat");
    }


}
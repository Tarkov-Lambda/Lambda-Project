using Lambda.UI;
using Comfort.Common;
using EFT.UI;
using Lambda.Core.Main.AssetBundleHandling;
using Lambda.Core.Patches.Tarkov.UI;
using Lambda.Core.Patches.Tarkov.UI.QuickAccess;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Lambda.Core.Main.UI;

public class UIManager : IDisposable
{
    private const string UI_BUNDLE_NAME       =  "arenaui";
    private const string MATCH_UI_PREFAB_PATH =  "Packages/com.lambda.ui/ArenaMatchUI.prefab";
    private const string UI_MATTE_PATH        =  "Packages/com.lambda.ui/UIMatte.mat";

    private static string UIAssetBundlePath   => Path.Combine(Plugin.pathToBundles, UI_BUNDLE_NAME);

    private AssetBundle UIBundle;

    List<IDisposable> disposables = new();

    ArenaMatchUI matchUI;

    public UIManager()
    {
        if (H.IsHeadless) return;

        H.AfterApplicationLoaded += Initialize;

        // hot reload
        if (H.IsMainMenuLoaded()) Initialize();
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

        UIBundle.Unload(false);
    }

    void LoadUI(CommonUI commonUI)
    {
        Plugin.Logger.LogInfo("Loading UIManager");

        BSGItemInfoProvider itemInfoProvider = new BSGItemInfoProvider();

        UIBundle = AssetBundle.LoadFromFile(UIAssetBundlePath);

        GameObject prefabMatchUI = UIBundle.LoadAsset<GameObject>(MATCH_UI_PREFAB_PATH);
        matchUI = GameObject.Instantiate(prefabMatchUI, commonUI.EftBattleUIScreen.transform).GetComponent<ArenaMatchUI>();
        matchUI.transform.SetAsFirstSibling();

        try
        {
            // disposables.Add(new LoadingScreenController(commonUI, UIBundle));

            disposables.Add(new ScoreboardController(matchUI.Scoreboard));
            disposables.Add(new TopBarController(matchUI.TopBar));
            disposables.Add(new KillFeedController(matchUI.KillFeed, itemInfoProvider));
            disposables.Add(new MatchResultController(matchUI.PopupMatchEnd));
            disposables.Add(new SelfDeathController(matchUI.DeathInfo));
            disposables.Add(new SpectatorController(matchUI.Spectator));
            disposables.Add(new ChatController(matchUI.Chat));

            disposables.Add(new ShopUIController(commonUI, UIBundle, itemInfoProvider));
            disposables.Add(new NameplateController(commonUI, UIBundle));
            disposables.Add(new EditBuildController(commonUI, UIBundle));

            disposables.Add(new FactionSelectionController(commonUI, UIBundle));
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError(e);
        }

        PatchGroup_QuickAccessPanel_ModifyItemIcon.MatteMaterial = UIBundle.LoadAsset<Material>(UI_MATTE_PATH);
    }


}
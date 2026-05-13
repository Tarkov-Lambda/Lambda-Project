using BepInEx;
using SPT.Reflection.Patching;
using System.Collections.Generic;
using Comfort.Common;

using Lambda.Core.Patches.Tarkov.UI.BattleStance;
using Lambda.Core.Patches.Tarkov.UI.QuickAccess;
using EFT.UI;
using Lambda.Core.Patches.Tarkov.UI.WeaponBuilds;
using Lambda.Core.Patches.Tarkov.UI.QuickAccess.MagazineSelector;

namespace Lambda.Core.Patches.Tarkov.UI;

internal static class UIPatches
{
    private static readonly Stack<ModulePatch> patches = new();
    private static readonly Stack<PatchGroup> patchGroups = new();

    internal static void RegisterAndEnable(PatchGroup patchGroup)
    {
        patchGroups.Push(patchGroup);
        patchGroups.Peek().Enable();
    }
    internal static void RegisterAndEnable(ModulePatch patch)
    {
        patches.Push(patch);
        patches.Peek().Enable();
    }

    internal static void Enable()
    {
        Disable();

        RegisterAndEnable(new Patch_PreloaderUI_RefreshCornerLabel());
        if (Singleton<PreloaderUI>.Instantiated)
            H.PreloaderUI.method_6(); // PreloaderUI.RefreshCornerLabel();

        RegisterAndEnable(new Patch_CommonUI_Awake());

        // Inventory opening control (for when we reset inv or hold tab for scoreboard)
        RegisterAndEnable(new Patch_ItemsTabController_Show());
        RegisterAndEnable(new Patch_EftGamePlayerOwner_TranslateInventoryScreenInput());
        RegisterAndEnable(new Patch_EftGamePlayerOwner_BlockScrollDuringMagSelect());

        RegisterAndEnable(new Patch_BattleStancePanel_Awake());

        RegisterAndEnable(new Patch_InventoryScreenQuickAccessPanel_Show());

        RegisterAndEnable(new Patch_QuickSlotView_SwitchVisualSelection());

        RegisterAndEnable(new PatchGroup_GrenadeSelector_NewLook());

        RegisterAndEnable(new PatchGroup_QuickAccessPanel_HideEmptySlots());
        RegisterAndEnable(new PatchGroup_QuickAccessPanel_HideItemBG());
        RegisterAndEnable(new PatchGroup_QuickAccessPanel_ModifyItemIcon());

        RegisterAndEnable(new PatchGroup_AmmoSelector_NewLook());

        if (Singleton<CommonUI>.Instantiated)
        {
            Patch_CommonUI_Awake.ModifyQuickAccessPanel(Singleton<CommonUI>.Instance);
            Patch_CommonUI_Awake.StretchInventoryScreen(Singleton<CommonUI>.Instance);
        }


        RegisterAndEnable(new Patch_EditBuildScreen_Show());
        RegisterAndEnable(new Patch_EditBuildScreen_UpdateItem());

        RegisterAndEnable(new Patch_OpenBuildWindow_Show());
        RegisterAndEnable(new Patch_CategoryView_Show());
        RegisterAndEnable(new Patch_SubcategoryView_Show());
    }

    internal static void Disable()
    {
        while (patches.Count > 0)
            patches.Pop().Disable();

        while (patchGroups.Count > 0)
            patchGroups.Pop().Disable();
    }
}
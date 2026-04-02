using BepInEx;
using SPT.Reflection.Patching;
using System.Collections.Generic;
using Comfort.Common;

using ifp.arena.bep.Patches.Tarkov.UI.BattleStance;
using ifp.arena.bep.Patches.Tarkov.UI.QuickAccess;

namespace ifp.arena.bep.Patches.Tarkov.UI
{
    internal static class UIPatches
    {
        private static readonly List<ModulePatch> patches = new();

        static void RegisterPatch(ModulePatch patch)
        {
            patch.Enable();
            patches.Add(patch);
        }

        internal static void Enable()
        {
            RegisterPatch(new Patch_CommonUI_Awake());                                  // UI Action Hook
            RegisterPatch(new Patch_ItemsTabController_Show());                         // UI Action Hook
            RegisterPatch(new Patch_EftGamePlayerOwner_TranslateInventoryScreenInput());// Inventory opening control (for when we reset inv or hold tab for scoreboard)

            RegisterPatch(new Patch_BattleStancePanel_Awake());

            RegisterPatch(new Patch_BoundSlotView_Show());
            RegisterPatch(new Patch_BoundItemView_Show());
            RegisterPatch(new Patch_QuickSlotView_ShowInfoPanel());

            RegisterPatch(new Patch_GrenadeSelector_Awake());

            RegisterPatch(new Patch_QuickSlotItemView_UpdateScale()); // remove icon rotation

            if (Singleton<EFT.UI.CommonUI>.Instantiated)
            {
                Patch_CommonUI_Awake.ModifyQuickAccessPanel(Singleton<EFT.UI.CommonUI>.Instance);
            }
        }

        internal static void Disable()
        {
            foreach (var patch in patches)
            {
                patch.Disable();
            }
            patches.Clear();
        }
    }
}

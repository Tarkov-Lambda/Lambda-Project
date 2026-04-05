using DG.Tweening;
using EFT.UI.DragAndDrop;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;
using UnityEngine;

namespace ifp.arena.bep.Patches.Tarkov.UI.QuickAccess;

internal class Patch_QuickSlotView_SwitchVisualSelection : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(QuickSlotView), nameof(QuickSlotView.SwitchVisualSelection));
    }

    [PatchPostfix]
    private static void PatchPostfix(QuickSlotView __instance, bool selected, CustomTextMeshProUGUI ___Caption)
    {
        Color color = selected ? Color.white : new Color(0, 0, 0, 0f);

        ___Caption.color = color;

        __instance.GetOrAddComponent<CanvasGroup>().DOFade(selected ? 0.8f : 0.2f, 0.3f);
    }
}
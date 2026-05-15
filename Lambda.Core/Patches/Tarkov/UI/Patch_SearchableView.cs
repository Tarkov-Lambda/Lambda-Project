using System.Reflection;
using EFT.UI;
using EFT.UI.DragAndDrop;
using HarmonyLib;
using Lambda.Core.Main;
using SPT.Reflection.Patching;

namespace Lambda.Core.Patches.Tarkov.UI;

internal class Patch_SearchableView_Awake : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(SearchableView), nameof(SearchableView.Start));

    public static bool IsNewProfile { get; private set; }

    [PatchPostfix]
    static void Postfix(SearchableView __instance)
    {
        if (!H.IsInRaid()) return;
        if (__instance.gameObject.name == "SecuredContainer Slot")
        {
            __instance.gameObject.SetActive(false);
        }
    }
}
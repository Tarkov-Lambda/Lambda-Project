using System.Reflection;
using EFT.UI;
using HarmonyLib;
using ifp.arena.bep.Core;
using SPT.Reflection.Patching;

namespace ifp.arena.bep.Patches.Tarkov.UI;

internal class Patch_LoginUI_Awake : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(LoginUI), nameof(LoginUI.Awake));

    [PatchPostfix]
    static void Postfix()
    {
        DefaultEquipmentManager.Instance.isNewProfile = true;
        D.Log("Fresh Start");
    }
}
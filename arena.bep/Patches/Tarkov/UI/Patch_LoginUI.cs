using System.Reflection;
using EFT.UI;
using HarmonyLib;
using ifp.arena.bep.Core;
using SPT.Reflection.Patching;

namespace ifp.arena.bep.Patches.Tarkov.UI;

internal class Patch_LoginUI_Awake : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(LoginUI), nameof(LoginUI.Awake));

    public static bool IsNewProfile { get; private set; }

    [PatchPostfix]
    static void Postfix()
    {
        // IsNewProfile = true;
    }
}
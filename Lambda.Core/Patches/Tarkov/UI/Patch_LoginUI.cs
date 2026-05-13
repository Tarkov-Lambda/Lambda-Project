using System.Reflection;
using EFT.UI;
using HarmonyLib;
using Lambda.Core.Main;
using SPT.Reflection.Patching;

namespace Lambda.Core.Patches.Tarkov.UI;

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
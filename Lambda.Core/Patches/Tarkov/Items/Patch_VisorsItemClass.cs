using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;

namespace Lambda.Core.Patches.Tarkov;

internal class Patch_VisorsItemClass_Constructor : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Constructor(typeof(VisorsItemClass), [typeof(string), typeof(VisorsTemplateClass)]);

    [PatchPrefix]
    static bool Prefix(VisorsItemClass __instance, string id, ref VisorsTemplateClass template)
    {
        template.BlindnessProtection = 0.91f;
        template.BlocksHeadwear = false;
        template.BlocksFaceCover = false;

        return true;
    }
}
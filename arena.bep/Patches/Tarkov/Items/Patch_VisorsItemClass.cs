using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;

namespace ifp.arena.bep.Patches.Tarkov;

public class Patch_VisorsItemClass_Constructor : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Constructor(typeof(VisorsItemClass), [typeof(string), typeof(VisorsTemplateClass)]);

    [PatchPrefix]
    static bool Prefix(VisorsItemClass __instance, string id, ref VisorsTemplateClass template)
    {
        template.BlindnessProtection = 0.94f;
        template.BlocksHeadwear = false;
        template.BlocksFaceCover = false;

        return true;
    }
}
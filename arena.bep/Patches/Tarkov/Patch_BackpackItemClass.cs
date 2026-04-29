using HarmonyLib;
using ifp.arena.bep.Core.Gamemode;
using SPT.Reflection.Patching;
using System;
using System.Reflection;

namespace ifp.arena.bep.Patches.Tarkov;


public class Patch_BackpackItemClass_Constructor : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Constructor(typeof(BackpackItemClass), [typeof(string), typeof(BackpackTemplateClass)]);

    [PatchPrefix]
    static bool Prefix(BackpackItemClass __instance, string id, ref BackpackTemplateClass template)
    {
        if (SNDGamemode.bombTemplateId == template.StringId)
        {
            template.Grids = [];
            template.mousePenalty = 0f;
        }
        return true;
    }
}

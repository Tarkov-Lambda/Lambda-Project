using HarmonyLib;
using Lambda.Core.Main.Gamemode;
using SPT.Reflection.Patching;
using System;
using System.Reflection;

namespace Lambda.Core.Patches.Tarkov;

public class Patch_BackpackItemClass_Constructor : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Constructor(typeof(BackpackItemClass), [typeof(string), typeof(BackpackTemplateClass)]);

    [PatchPrefix]
    static bool Prefix(BackpackItemClass __instance, string id, ref BackpackTemplateClass template)
    {
        if (Hardcode.BOMB_BACKPACK == template.StringId)
        {
            template.Grids = [];
            template.mousePenalty = 0f;
        }
        return true;
    }
}

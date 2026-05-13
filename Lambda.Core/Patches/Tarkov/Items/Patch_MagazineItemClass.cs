using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;

namespace Lambda.Core.Patches.Tarkov;

public class Patch_MagazineItemClass_GetAmmoCountByLevel : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(MagazineItemClass), nameof(MagazineItemClass.GetAmmoCountByLevel));

    [PatchPrefix]
    static bool Prefix(ref bool @checked, ref int skill)
    {
        @checked = true;
        skill = 2;
        return true;
    }
}
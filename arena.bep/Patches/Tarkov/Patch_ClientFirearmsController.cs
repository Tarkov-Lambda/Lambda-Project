using EFT;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;

namespace ifp.arena.bep.Patches.Tarkov;

public class Patch_ClientFirearmController_CanPressTrigger : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.PropertyGetter(typeof(ClientFirearmController), nameof(ClientFirearmController.IsTriggerPressed));

    [PatchPrefix]
    static bool Prefix(ref bool __result)
    {
        if (H.MainPlayerScore.IsControllerPartiallyLocked())
        {
            __result = false;
            return false;
        }

        return true;
    }
}


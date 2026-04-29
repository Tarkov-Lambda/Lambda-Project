using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;
using ifp.arena.bep.Core;

namespace ifp.arena.bep.Patches.Tarkov;

public class Patch_SmokeGrenade_Init : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(SmokeGrenade), nameof(SmokeGrenade.Init), 
    [typeof(GrenadeSettings), typeof(string), typeof(ThrowWeapItemClass), typeof(float), typeof(ISharedBallisticsCalculator), typeof(bool)]);

    [PatchPostfix]
    static void Postfix(SmokeGrenade __instance, GrenadeSettings settings, string profileId, ThrowWeapItemClass throwWeap, float timeSpent, ISharedBallisticsCalculator calculator, bool isBeingPlanted)
    {
        if (H.IsServer)
        {
            __instance.StartVelocityGateAsync().Forget();
        }
    }
}
using Comfort.Common;
using Cysharp.Threading.Tasks;
using EFT;
using HarmonyLib;
using ifp.arena.bep.networking;
using SPT.Reflection.Patching;
using System.Reflection;

namespace ifp.arena.bep.Patches.Tarkov;

public class Patch_Grenade_Init : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(Grenade), nameof(Grenade.Init), [typeof(GrenadeSettings), typeof(string), typeof(ThrowWeapItemClass), typeof(float), typeof(ISharedBallisticsCalculator), typeof(bool)]);

    [PatchPostfix]
    static void Prefix(Grenade __instance)
    {
        if (__instance.WeaponSource.StringTemplateId is Hardcode.MOLOTOV_GRENADE)
        {
            UniTask.RunOnThreadPool(async () =>
            {
                await UniTask.Delay(950);
                if (__instance != null)
                {
                    __instance.InvokeBlowUpEvent();
                }
            }).Forget();
        }
    }
}

public class Patch_Grenade_InvokeBlowUpEvent : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(Grenade), nameof(Grenade.InvokeBlowUpEvent));

    [PatchPrefix]
    static void Prefix(Grenade __instance)
    {
        if (H.IsServer && __instance.WeaponSource.StringTemplateId is Hardcode.MOLOTOV_GRENADE)
        {
            Singleton<MolotovExplosionPacketHandler>.Instance.Send(__instance.transform.position);
        }
    }
}
using Comfort.Common;
using Cysharp.Threading.Tasks;
using EFT;
using HarmonyLib;
using Lambda.Core.Networking;
using SPT.Reflection.Patching;
using System.Reflection;

namespace Lambda.Core.Patches.Tarkov;

internal class Patch_Grenade_Init : ModulePatch
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

internal class Patch_Grenade_InvokeBlowUpEvent : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(Grenade), nameof(Grenade.InvokeBlowUpEvent));

    [PatchPrefix]
    static void Prefix(Grenade __instance)
    {
        if (H.IsServer && __instance.WeaponSource.StringTemplateId is Hardcode.MOLOTOV_GRENADE)
        {
            Singleton<MolotovExplosionPacketWarden>.Instance.Send(__instance.transform.position);
        }
    }
}
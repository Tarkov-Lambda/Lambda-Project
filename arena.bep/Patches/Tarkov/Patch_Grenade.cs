using Comfort.Common;
using EFT;
using Fika.Core.Main.Utils;
using HarmonyLib;
using ifp.arena.bep.networking;
using SPT.Reflection.Patching;
using System.Reflection;
using Systems.Effects;
using UnityEngine;

namespace ifp.arena.bep.Patches.Tarkov;

public class Patch_Grenade_InvokeBlowUpEvent : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(Grenade), nameof(Grenade.InvokeBlowUpEvent));

    [PatchPrefix]
    static bool Prefix(Grenade __instance)
    {
        if (__instance.WeaponSource.StringTemplateId is Hardcode.MOLOTOV_GRENADE)
        {
            // if (!string.IsNullOrEmpty(__instance.WeaponSource.ExplosionEffectType))
            // {
            //     H.Effects.EmitGrenade(__instance.WeaponSource.ExplosionEffectType, __instance.transform.position, Vector3.up, 0f);
            // }

            // __instance.method_4();

            if (H.IsServer)
            {
                Singleton<MolotovExplosionPacketHandler>.Instance.Send(__instance.transform.position);
            }

            // UnityEngine.Object.DestroyImmediate(__instance.gameObject);
            return true;
        }

        return true;
    }
}
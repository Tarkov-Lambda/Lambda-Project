using EFT;
using Fika.Core.Main.Players;
using SPT.Reflection.Patching;
using System;
using System.Reflection;
using UnityEngine;

namespace ifp.arena.bep.Patches;

public class ObservedPlayer_POV_Getter_Patch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return typeof(ObservedPlayer).GetProperty(nameof(ObservedPlayer.PointOfView)).GetGetMethod();
    }

    [PatchPrefix]
    public static bool Prefix(ObservedPlayer __instance, ref EPointOfView __result)
    {
        if (__instance.PlayerBody != null && __instance.PlayerBody.PointOfView.Value == EPointOfView.FirstPerson)
        {
            __result = EPointOfView.FirstPerson;
            return false;
        }
        return true;
    }
}

public class ObservedPlayer_VisualPass_Patch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return typeof(ObservedPlayer).GetMethod("ObservedVisualPass", BindingFlags.Instance | BindingFlags.NonPublic, null, new Type[] { typeof(float), typeof(int) }, null);
    }

    [PatchPrefix]
    public static bool Prefix(ObservedPlayer __instance, float deltaTime)
    {
        if (__instance.PlayerBody.PointOfView.Value == EPointOfView.FirstPerson)
        {
            var pwa = __instance.ProceduralWeaponAnimation;
            var mc = __instance.MovementContext;

            pwa.SmoothedTilt = Mathf.Lerp(pwa.SmoothedTilt, mc.Tilt, deltaTime * 3f);
            pwa.UpdatePossibleTilt(mc.SmoothedCharacterMovementSpeed, mc.SmoothedPoseLevel);
        }

        return true;
    }
}
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using Fika.Core.Main.Players;
using HarmonyLib;
using Lambda.Core.Networking;
using SPT.Reflection.Patching;
using System;
using System.Reflection;
using UnityEngine;

namespace Lambda.Core.Patches;

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

public class Patch_Player_FindItemById : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(Player), nameof(Player.FindItemById));

    [PatchPrefix]
    public static bool Prefix(Player __instance, ref GStruct156<Item> __result, MongoID itemId, bool checkDistance = true, bool checkOwnership = true)
    {
        if (__instance is ObservedPlayer)
        {
            GStruct156<ValueTuple<Item, GameWorld.GStruct162>> gstruct = __instance.GameWorld.FindItemWithWorldData(itemId);
            if (gstruct.Failed)
            {
                __result = gstruct.Error;
                Singleton<EquipmentResyncPacketWarden>.Instance.Send(__instance);
            }
            __result = gstruct.Value.Item1;

            return false;
        }
        else return true;
    }
}
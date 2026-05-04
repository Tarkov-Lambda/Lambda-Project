using System.Reflection;
using Diz.Skinning;
using EFT;
using HarmonyLib;
using SPT.Reflection.Patching;
using UnityEngine.Rendering;

namespace ifp.arena.bep.Patches.Tarkov;

public class Patch_PlayerBody_UpdatePlayerRenders : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(PlayerBody), nameof(PlayerBody.UpdatePlayerRenders));

    [PatchPrefix]
    static bool Prefix(PlayerBody __instance, PluggableBone ____watches, EPointOfView pointOfView, EPlayerSide side)
    {
        bool isFirstPerson = pointOfView is EPointOfView.FirstPerson;

        foreach (var (eBodyModelPart2, loddedSkin2) in __instance.BodySkins)
        {
            switch (eBodyModelPart2)
            {
                case EBodyModelPart.Hands:
                    loddedSkin2.EnableRenderers(isFirstPerson);
                    break;
                default:
                    loddedSkin2.SetShadowCastingMode((pointOfView == EPointOfView.ThirdPerson) ? ShadowCastingMode.On : ShadowCastingMode.ShadowsOnly);
                    break;
                case EBodyModelPart.Feet:
                    break;
            }
        }
        if (____watches != null)
        {
            ____watches.gameObject.SetActive(isFirstPerson);
        }
        __instance.PointOfView.Value = pointOfView;
        __instance.PlayerSide.Value = side;
        return false;
    }
}
using System.Reflection;
using Comfort.Common;
using EFT;
using HarmonyLib;
using Lambda.Core.Networking;
using SPT.Reflection.Patching;
using static EFT.Player;

namespace Lambda.Core.Patches.Tarkov;

internal class Patch_EmptyHandsController_ExamineWeapon : ModulePatch
{
    protected override MethodBase GetTargetMethod() =>
        AccessTools.Method(
            typeof(EmptyHandsController),
            nameof(EmptyHandsController.ExamineWeapon)
        );

    [PatchPostfix]
    public static void Postfix(EmptyHandsController __instance, Player ____player)
    {
        if (____player.IsYourPlayer)
        {
            Singleton<HandsInspectPacketWarden>.Instance.Send();
        }
    }
}

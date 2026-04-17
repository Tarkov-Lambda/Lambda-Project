using HarmonyLib;
using EFT;
using static EFT.Player;
using Comfort.Common;
using ifp.arena.bep.networking;
using SPT.Reflection.Patching;
using System.Reflection;

namespace ifp.arena.bep.Patches.Tarkov;

    internal class Patch_EmptyHandsController_ExamineWeapon : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(EmptyHandsController), nameof(EmptyHandsController.ExamineWeapon));

        [PatchPostfix]
        public static void Postfix(EmptyHandsController __instance, Player ____player)
        {
            if (____player.IsYourPlayer)
            {
                Singleton<HandsInspectPacketHandler>.Instance.Send();
            }
        }
    }
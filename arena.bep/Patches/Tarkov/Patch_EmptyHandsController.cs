using HarmonyLib;
using EFT;
using static EFT.Player;
using Comfort.Common;
using ifp.arena.bep.networking;
using SPT.Reflection.Patching;
using System.Reflection;
using ifp.arena.bep.Core;

namespace ifp.arena.bep.Patches.Tarkov
{
    internal class Patch_EmptyHandsController_ExamineWeapon : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(EmptyHandsController), nameof(EmptyHandsController.ExamineWeapon));
        }

        [PatchPostfix]
        public static void Postfix(EmptyHandsController __instance)
        {
            Player player = AccessTools.Field(__instance.GetType(), "_player").GetValue(__instance) as Player;

            if (player != null && player.IsYourPlayer)
            {
                Singleton<HandsInspectPacketHandler>.Instance.Send(player.Id);
            }
        }
    }
}
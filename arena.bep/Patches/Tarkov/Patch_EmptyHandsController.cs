using HarmonyLib;
using EFT;
using static EFT.Player;
using Comfort.Common;
using ifp.arena.bep.networking;
using SPT.Reflection.Patching;
using System.Reflection;
using ifp.arena.bep.Core;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ifp.arena.bep.Patches.Tarkov
{
    internal class Patch_EmptyHandsController_ExamineWeapon : ModulePatch
    {
        private static readonly AccessTools.FieldRef<EmptyHandsController, Player> playerRef = AccessTools.FieldRefAccess<EmptyHandsController, Player>("_player");

        protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(EmptyHandsController), nameof(EmptyHandsController.ExamineWeapon));

        [PatchPostfix]
        public static void Postfix(EmptyHandsController __instance)
        {
            Player player = playerRef(__instance);

            if (player.IsYourPlayer)
            {
                Singleton<HandsInspectPacketHandler>.Instance.Send(player.Id);
            }
        }
    }
}


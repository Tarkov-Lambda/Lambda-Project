using Comfort.Common;
using EFT;
using EFT.HealthSystem;
using EFT.UI;
using Fika.Core.Main.Components;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.Packets.Player.Common;
using HarmonyLib;
using ifp.arena.bep.GameTypes;
using ifp.arena.bep.Networking;
using SPT.Reflection.Patching;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

namespace ifp.arena.bep.Patches
{
    public class Patch_CanWalk : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.PropertyGetter(typeof(MovementContext), "CanWalk");
        }

        [PatchPostfix]
        static void Postfix(ref bool __result)
        {
            if (Singleton<BaseGameMode>.Instance.sessionInfo.roundState == RoundState.Warmup)
            {
                __result = false;
            }
        }
    }
}

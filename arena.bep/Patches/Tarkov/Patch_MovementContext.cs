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
using ifp.arena.bep.networking;
using SPT.Reflection.Patching;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

namespace ifp.arena.bep.Patches.Tarkov
{
    public class Patch_CanWalk : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.PropertyGetter(typeof(MovementContext), nameof(MovementContext.CanWalk));
        }

        [PatchPostfix]
        static void Postfix(ref bool __result)
        {
            if (Singleton<BaseGameMode>.Instance.session.IsControllerPartiallyLocked())
            {
                __result = false;
            }
        }
    }

    public class Patch_CanJump : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.PropertyGetter(typeof(MovementContext), nameof(MovementContext.CanJump));
        }

        [PatchPostfix]
        static void Postfix(ref bool __result)
        {
            if (Singleton<BaseGameMode>.Instance.session.IsControllerPartiallyLocked())
            {
                __result = false;
            }
        }
    }
}

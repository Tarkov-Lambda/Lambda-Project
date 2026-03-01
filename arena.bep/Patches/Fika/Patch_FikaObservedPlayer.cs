using Comfort.Common;
using EFT;
using EFT.HealthSystem;
using Fika.Core.Main.Players;
using Fika.Core.Main.Utils;
using Fika.Core.Networking;
using HarmonyLib;
using ifp.arena.bep.Core.Dying;
using ifp.arena.bep.Networking;
using SPT.Reflection.Patching;
using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

namespace ifp.arena.bep.Patches.Fika
{
    public class Patch_ApplyShot : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(EFT.Player), nameof(EFT.Player.ShotReactions));
        }

        [PatchPostfix]
        static void Postfix(Player __instance, DamageInfoStruct shot, EBodyPart bodyPart)
        {
            if (FikaBackendUtils.IsHeadless) return;

            if (shot.Player.Nickname == Singleton<GameWorld>.Instance.MainPlayer.name && bodyPart == EBodyPart.Head)
            {
                Plugin.Logger.LogInfo("Headshot");
            }
        }
    }
}

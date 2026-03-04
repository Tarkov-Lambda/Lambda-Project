using EFT;
using Fika.Core.Main.Utils;
using HarmonyLib;
using ifp.arena.bep.Core;
using SPT.Reflection.Patching;
using System.Reflection;

namespace ifp.arena.bep.Patches.Fika
{
    public class Patch_ApplyShot : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(Player), nameof(Player.ShotReactions));
        }

        [PatchPostfix]
        static void Postfix(DamageInfoStruct shot, EBodyPart bodyPart)
        {
            if (FikaBackendUtils.IsHeadless) return;
            
            if (shot.Player.iPlayer.Id == H.gameWorld.MainPlayer.Id && bodyPart == EBodyPart.Head)
            {
                // Play
                H.Notify($" {shot.Player.iPlayer.Id} {H.gameWorld.MainPlayer.Id}");
            }
        }
    }
}
            

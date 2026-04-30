using BepInEx.Configuration;
using Fika.Core;
using Fika.Core.Main.Components;
using Fika.Core.Main.Players;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;

namespace ifp.arena.bep.Patches
{
    internal class Patch_FikaHealthBar_Create : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(FikaHealthBar), nameof(FikaHealthBar.Create));
        
        [PatchPrefix]
        static bool Prefix(ref FikaHealthBar __result)
        {
            __result = null;
            return false;
        }
    }
}

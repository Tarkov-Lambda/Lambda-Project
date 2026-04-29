using BepInEx.Configuration;
using Fika.Core;
using Fika.Core.Main.Components;
using Fika.Core.Main.Players;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;

namespace ifp.arena.bep.Patches
{
    internal class Patch_FikaHealthBar_UseNamePlates : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.PropertyGetter(typeof(FikaConfig), nameof(FikaConfig.UseNamePlates));
        }

        [PatchPrefix]
        static bool Prefix(ref ConfigEntry<bool> __result)
        {
            __result.Value = false;
            return false;
        }
    }
}

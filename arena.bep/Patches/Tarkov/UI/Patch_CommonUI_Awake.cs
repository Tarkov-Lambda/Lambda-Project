using EFT.HealthSystem;
using EFT.UI;
using HarmonyLib;
using SPT.Reflection.Patching;
using System;
using System.Reflection;

namespace ifp.arena.bep.Patches.Tarkov.UI
{
    internal class Patch_CommonUI_Awake : ModulePatch
    {
        public static event Action<CommonUI> OnAwake;

        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(CommonUI), nameof(CommonUI.Awake));
        }

        [PatchPostfix]
        static void Postfix(CommonUI __instance)
        {
            OnAwake?.Invoke(__instance);
        }
    }
}
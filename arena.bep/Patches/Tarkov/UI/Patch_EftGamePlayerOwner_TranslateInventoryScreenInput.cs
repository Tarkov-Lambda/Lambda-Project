using System.Linq;
using System.Reflection;
using Comfort.Common;
using EFT;
using EFT.InputSystem;
using HarmonyLib;
using ifp.arena.bep.Core;
using ifp.arena.bep.Core.UI;
using SPT.Reflection.Patching;
using UnityEngine;
using static EFT.InputSystem.InputNode;

namespace ifp.arena.bep.Patches.Tarkov.UI
{
    internal class Patch_EftGamePlayerOwner_TranslateInventoryScreenInput : ModulePatch
    {
        public static bool AllowOpenInventory = false;

        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(EftGamePlayerOwner), nameof(EftGamePlayerOwner.TranslateInventoryScreenInput));
        }

        [PatchPrefix]
        static bool Prefix(EftGamePlayerOwner __instance, ECommand command, ref bool __result)
        {
            if (command == ECommand.ToggleInventory)
            {
                if (AllowOpenInventory && H.MainPlayerScore.isAlive)
                {
                    AllowOpenInventory = false;
                    return true;
                }

                __result = true;
                return false;
            }

            return true;
        }
    }
}
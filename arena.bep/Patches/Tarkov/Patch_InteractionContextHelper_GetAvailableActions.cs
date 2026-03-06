using EFT;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;
using ifp.arena.shared;
using System;

using InteractionContextHelper = GetActionsClass;
using IInteractive = GInterface177;

namespace ifp.arena.bep.Patches.Tarkov
{
    internal class Patch_InteractionContextHelper_GetAvailableActions : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(InteractionContextHelper), nameof(InteractionContextHelper.GetAvailableActions), [typeof(GamePlayerOwner), typeof(IInteractive)]);
        }

        [PatchPrefix]
        private static bool PatchPrefix(ref ActionsReturnClass __result, GamePlayerOwner owner, IInteractive interactive)
        {
            BombPlantZone plantZone = interactive as BombPlantZone;
            if (plantZone == null)
                return true;

            ActionsReturnClass actionsReturnClass = new ActionsReturnClass();
            actionsReturnClass.Actions.Add(new ActionsTypesClass
            {
                Name = "Plant",
                Action = new Action(Plant)
            });

            return false;
        }

        static void Plant()
        {
        }
    }
}

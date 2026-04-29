using System.Reflection;
using System.Text.RegularExpressions;
using EFT;
using EFT.InputSystem;
using HarmonyLib;
using ifp.arena.bep.Core.Gamemode;
using SPT.Reflection.Patching;

namespace ifp.arena.bep.Patches.Tarkov.UI;

internal class Patch_EftGamePlayerOwner_TranslateInventoryScreenInput : ModulePatch
{
    public static bool AllowOpenInventory = false;

    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(EftGamePlayerOwner), nameof(EftGamePlayerOwner.TranslateInventoryScreenInput));

    [PatchPrefix]
    static bool Prefix(EftGamePlayerOwner __instance, ECommand command, ref bool __result)
    {
        if (command == ECommand.ToggleInventory)
        {
            if (H.Session.matchState
                is MatchState.SideSwap
                or MatchState.MatchEnd
                or MatchState.Warmup
                or MatchState.WarmupEnd
                or MatchState.None
                or MatchState.Cleanup)
            {
                AllowOpenInventory = false;
                __result = true;
                return false;
            }

            bool wasAllowed = AllowOpenInventory;
            AllowOpenInventory = false;

            if (wasAllowed && H.MainPlayerScore.IsAlive)
            {
                return true;
            }


            __result = true;
            return false;
        }

        return true;
    }
}
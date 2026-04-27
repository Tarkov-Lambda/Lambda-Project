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

            // TODO: REFACTOR
            // bruteforce disallow inventory to open during these states
            if (H.Session.matchState
            is MatchState.SideSwap
            or MatchState.MatchEnd
            or MatchState.None
            or MatchState.Cleanup)
            {
                AllowOpenInventory = false;
                return true;
            }

            if (AllowOpenInventory && H.MainPlayerScore.IsAlive && !InventoryResetter.IsResetting)
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
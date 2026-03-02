using EFT;
using HarmonyLib;
using ifp.arena.bep.networking.TimeSync;
using SPT.Reflection.Patching;
using System;
using System.Reflection;

namespace ifp.arena.bep.Patches.Tarkov
{
    internal class Patch_Gameworld_OnGameStarted : ModulePatch
    {
        public static event Action<GameWorld> OnGameStarted;

        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(GameWorld), nameof(GameWorld.OnGameStarted));
        }

        
        [PatchPostfix]
        static void Postfix(GameWorld __instance)
        {
            // Reset time sync on every game start so reconnects / raids don't reuse stale offsets.
            NetworkTime.Reset();
            OnGameStarted?.Invoke(__instance);
        }
    }
}

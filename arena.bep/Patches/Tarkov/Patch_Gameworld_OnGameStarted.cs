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
            if(__instance is HideoutGameWorld) return;

            // Reset time sync on every game start so reconnects / raids don't reuse stale offsets.
            NetworkTime.Reset();
            OnGameStarted?.Invoke(__instance);
        }
    }

    internal class Patch_Gameworld_OnDispose : ModulePatch
    {
        public static event Action<GameWorld> OnDispose;

        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(GameWorld), nameof(GameWorld.Dispose));
        }


        [PatchPostfix]
        static void Postfix(GameWorld __instance)
        {
            if(__instance is HideoutGameWorld) return;

            // Reset time sync on every game start so reconnects / raids don't reuse stale offsets.
            NetworkTime.Reset();
            OnDispose?.Invoke(__instance);
        }
    }
}

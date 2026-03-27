using Comfort.Common;
using EFT;
using EFT.Animations;
using EFT.HealthSystem;
using EFT.UI;
using Fika.Core.Main.Components;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.Packets.Player.Common;
using HarmonyLib;
using ifp.arena.bep.Core;
using ifp.arena.bep.Core.Gamemode;
using ifp.arena.bep.networking;
using SPT.Reflection.Patching;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using UnityEngine;

namespace ifp.arena.bep.Patches.Tarkov
{
    // For Patch_ProceduralWeaponAnimation_ProcessEffectors
    public class Patch_Player_VisualPass : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(Player), nameof(Player.VisualPass));

        public static readonly ConditionalWeakTable<ProceduralWeaponAnimation, Player> PwaToPlayer = new ConditionalWeakTable<ProceduralWeaponAnimation, Player>();

        [PatchPrefix]
        static void Prefix(Player __instance)
        {
            var pwa = __instance.ProceduralWeaponAnimation;
            if (pwa != null)
            {
                PwaToPlayer.Remove(pwa);
                PwaToPlayer.Add(pwa, __instance);
            }
        }
    }

    public class Patch_Player_ShotReactions : ModulePatch, IDisposable
    {
        private static Dictionary<int, long> _lastShotTimeDict = new();
        private const int CooldownMs = 15;

        protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(Player), nameof(Player.ShotReactions));

        [PatchPostfix]
        static void Postfix(Player __instance, DamageInfoStruct shot, EBodyPart bodyPart)
        {
            D.Dump(shot);
            if(shot.OverDamageFrom is not null) return;
            if(bodyPart is not EBodyPart.Head) return;
            
            int killerId = shot.Player != null ? shot.Player.iPlayer.Id : 1;
            if (killerId != H.MainPlayer.Id) return;

            long now = Stopwatch.GetTimestamp();

            if (_lastShotTimeDict.TryGetValue(__instance.PlayerId, out long lastShotTime))
            {
                long elapsedMs = (now - lastShotTime) * 1000 / Stopwatch.Frequency;
                if (elapsedMs < CooldownMs) return;
            }

            // Only update AFTER passing cooldown (or if new)
            _lastShotTimeDict[__instance.PlayerId] = now;

            bool didHitHelmet = shot.BlockedBy is not null;

            AudioClip[] clips = didHitHelmet ? H.Sounds.HeadshotHelmet : H.Sounds.HeadshotFlesh;
            H.AudioHandler.PlayAtPoint(__instance.PlayerBody.PlayerBones.Head.position, clips.RandomElement(), 50, BetterAudio.AudioSourceGroupType.Character);
        }

        public void Dispose()
        {
            _lastShotTimeDict = null;
        }
    }
}

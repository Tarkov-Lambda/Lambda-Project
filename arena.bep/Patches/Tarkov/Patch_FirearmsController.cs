using Comfort.Common;
using EFT;
using EFT.HealthSystem;
using EFT.InventoryLogic;
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
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

namespace ifp.arena.bep.Patches.Tarkov
{
    public class Patch_FirearmController_Drop : ModulePatch
    {
        private const float DropAnimationSpeed = 1000f;

        protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(Player.FirearmController), nameof(Player.FirearmController.Drop));

        [PatchPrefix]
        static bool Prefix(ref float animationSpeed, Action callback, bool fastDrop, Item nextControllerItem)
        {
            animationSpeed = DropAnimationSpeed;
            return true;
        }
    }

    public class Patch_FirearmController_Spawn : ModulePatch
    {
        private const float DropAnimationSpeed = 3f;

        protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(Player.FirearmController), nameof(Player.FirearmController.Spawn));
        
        [PatchPrefix]
        static bool Prefix(ref float animationSpeed, Action callback)
        {
            animationSpeed = DropAnimationSpeed;
            return true;
        }
    }
}


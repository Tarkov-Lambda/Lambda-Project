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
    public struct MagAndAmmo
    {
        public MagazineItemClass magazine;
        public AmmoItemClass ammo;
    }

    public class Patch_FirearmController_InitiateShot : ModulePatch
    {
        public static Dictionary<Weapon, MagAndAmmo> AmmoRegistry = new Dictionary<Weapon, MagAndAmmo>();

        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(Player.FirearmController), nameof(Player.FirearmController.InitiateShot));
        }

        [PatchPostfix]
        private static void PatchPostfix(Player.FirearmController __instance, IWeapon weapon, AmmoItemClass ammo)
        {
            // if (H.MainPlayer.HandsController != __instance) return;

            Weapon weap = weapon.Item as Weapon;
            if (weap == null) return;

            AmmoRegistry[weap] = new MagAndAmmo
            {
                magazine = weap.GetCurrentMagazine(),
                ammo = ammo
            };
        }
    }


    public class Patch_FirearmController_Drop : ModulePatch
    {
        private const float DropAnimationSpeed = 3f;

        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(Player.FirearmController), nameof(Player.FirearmController.Drop));
        }

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

        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(Player.FirearmController), nameof(Player.FirearmController.Spawn));
        }

        [PatchPrefix]
        static bool Prefix(ref float animationSpeed, Action callback)
        {
            animationSpeed = DropAnimationSpeed;
            return true;
        }
    }

    public class Patch_FirearmController_InitiateOperation : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            // 1. Find the method
            MethodInfo openMethod = AccessTools.Method(typeof(EFT.Player.FirearmController), "InitiateOperation");

            // 2. Lock it to the specific animation class you want to intercept
            // Replace 'GClass2949' with the actual class you want to hook
            MethodInfo closedMethod = openMethod.MakeGenericMethod(typeof(GClass2949));

            return closedMethod;
        }

        [PatchPostfix]
        static void Postfix(object __result)
        {
            // Do whatever you want here when this specific animation starts
            H.Dump(__result);
        }
    }
}


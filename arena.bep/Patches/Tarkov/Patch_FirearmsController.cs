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
    public class Patch_CanPressTrigger : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(ClientFirearmController), nameof(ClientFirearmController.CanPressTrigger));
        }

        [PatchPrefix]
        static bool Prefix(ref bool __result)
        {
            if (Singleton<ArenaController>.Instance.session.IsControllerPartiallyLocked())
            {
                __result = false;
                return false;
            }

            return true;
        }
    }

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
}


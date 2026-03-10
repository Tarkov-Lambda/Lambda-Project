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
            return AccessTools.PropertyGetter(typeof(ClientFirearmController), nameof(ClientFirearmController.IsTriggerPressed));
        }

        [PatchPrefix]
        static bool Prefix(ref bool __result)
        {
            if (H.Session.IsControllerPartiallyLocked())
            {
                __result = false;
                return false;
            }

            return true;
        }
    }
}


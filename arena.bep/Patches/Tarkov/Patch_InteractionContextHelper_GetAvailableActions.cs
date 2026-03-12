using EFT;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;
using ifp.arena.shared;
using System;

using InteractionContextHelper = GetActionsClass;
using AvailableInteractionState = ActionsReturnClass;

using LocalizationExtensions = GClass2348;

using ArmorSlot = GClass3125;
using IInteractive = GInterface177;
using EFT.Interactive;
using EFT.InventoryLogic;
using System.Linq;
using ifp.arena.bep.Core.Gamemode;
using Comfort.Common;
using ifp.arena.bep.networking;
using UnityEngine;
using ifp.arena.bep.Core;

namespace ifp.arena.bep.Patches.Tarkov
{
    internal class Patch_InteractionContextHelper_GetAvailableActions : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(InteractionContextHelper), nameof(InteractionContextHelper.smethod_3));
        }

        [PatchPrefix]
        private static bool PatchPrefix(ref ActionsReturnClass __result, GamePlayerOwner owner, PlaceItemTrigger itemTrigger)
        {
            BombPlantZone plantZone = itemTrigger as BombPlantZone;
            if (plantZone == null)
                return true;

            if (H.Session.roundState != GameTypes.MatchState.RoundAction)
            {
                return true;
            }

            AvailableInteractionState actionsReturnClass = new AvailableInteractionState();

            Player player = owner.Player;

            Item bomb = FindBombItemInPlayer(player);
            if (bomb == null)
                return false;

            float plantingTime = SnDModeRules.platingTime;

            actionsReturnClass.Actions.Add(new ActionsTypesClass
            {
                Name = "PLANT",
                Action = delegate
                {
                    if (owner.Player.CurrentState is IdleStateClass)
                    {
                        Singleton<BombStatePacketHandler>.Instance.Send(owner.Player, GameTypes.BombState.Planting, GetBombPlantPosition(player));

                        owner.ShowObjectivesPanel("Planting {0:F1}", plantingTime);
                        owner.Player.CurrentManagedState.Plant(enabled: true, false, plantingTime, async (bool successful) =>
                        {
                            owner.Player.vmethod_6(bomb.TemplateId, itemTrigger.Id, successful);
                            owner.CloseObjectivesPanel();
                            if (!successful)
                            {
                                Singleton<BombStatePacketHandler>.Instance.Send(owner.Player, GameTypes.BombState.None, GetBombPlantPosition(player));
                                return;
                            }

                            // await ItemsUtils.ForceRemoveSlotAsync(EquipmentSlot.Backpack);
                            Singleton<BombStatePacketHandler>.Instance.Send(owner.Player, GameTypes.BombState.Planted, GetBombPlantPosition(player));
                            owner.ClearInteractionState();
                        });
                    }
                    else
                    {
                        owner.DisplayPreloaderUiNotification("You can't plant while moving");
                    }
                }
            });

            __result = actionsReturnClass;

            return false;
        }

        static Item FindBombItemInPlayer(Player player)
        {
            Item[] playerInventory = player.Profile.Inventory.GetPlayerItems(EPlayerItems.InRaidItems).ToArray();

            Item resultItem = playerInventory.FirstOrDefault((Item nextItem) => nextItem.TemplateId == SnDModeRules.bombTemplateId);

            return resultItem;
        }


        static Vector3 GetBombPlantPosition(Player player)
        {
            if (Physics.Raycast(player.Position, Vector3.down, out RaycastHit hit, 1f, 1 << 18))
            {
                return hit.point;
            }

            return player.Position;
        }
    }
}

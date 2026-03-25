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
using ifp.arena.bep.GameTypes;

namespace ifp.arena.bep.Patches.Tarkov
{
    internal class Patch_InteractionContextHelper_GetAvailableActions : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(InteractionContextHelper), nameof(InteractionContextHelper.smethod_3));

        [PatchPrefix]
        private static bool PatchPrefix(ref ActionsReturnClass __result, GamePlayerOwner owner, PlaceItemTrigger itemTrigger)
        {
            var roundState = H.Session.roundState;

            if (roundState != MatchState.RoundAction && roundState != MatchState.RoundPlanted)
                return true;

            Player player = owner.Player;
            AvailableInteractionState actionsReturnClass = new AvailableInteractionState();
            Item bomb = FindBombItemInPlayer(player);

            if (bomb != null && roundState == MatchState.RoundAction)
            {
                BombPlantZone plantZone = itemTrigger as BombPlantZone;
                if (plantZone == null)
                    return true;
                float plantingTime = SnDModeRules.platingTime;

                actionsReturnClass.Actions.Add(new ActionsTypesClass
                {
                    Name = "PLANT",
                    Action = delegate
                    {
                        if (owner.Player.CurrentState is IdleStateClass)
                        {
                            Singleton<BombStatePacketHandler>.Instance.Send(owner.Player, BombState.Planting, GetBombPlantPosition(player));

                            owner.ShowObjectivesPanel("Planting {0:F1}", plantingTime);
                            owner.Player.CurrentManagedState.Plant(enabled: true, false, plantingTime, async (bool successful) =>
                            {
                                owner.Player.vmethod_6(bomb.TemplateId, itemTrigger.Id, successful);
                                owner.CloseObjectivesPanel();
                                if (!successful)
                                {
                                    Singleton<BombStatePacketHandler>.Instance.Send(owner.Player, BombState.None, GetBombPlantPosition(player));
                                    return;
                                }

                                await IU.TryPopContainedItem(EquipmentSlot.Backpack, H.MainPlayer, false);
                                Singleton<BombStatePacketHandler>.Instance.Send(owner.Player, BombState.Planted, GetBombPlantPosition(player));
                                owner.ClearInteractionState();
                            });
                        }
                        else
                        {
                            owner.DisplayPreloaderUiNotification("You can't plant while moving");
                        }
                    }
                });
            }
            // bomb == null && H.MainPlayerScore?.faction != Faction.T &&
            else if ((H.Session.bombState == BombState.Planted || H.Session.bombState == BombState.Defusing) && Vector3.Distance(H.MainPlayer.Position, H.Arena.BombPlantedPosition) <= SnDModeRules.defuseRadius)
            {
                // RaycastHit hit;
                // Ray ray = CameraClass.Instance.Camera.ScreenPointToRay(Input.mousePosition);

                // if (Physics.Raycast(ray, out hit))
                // {
                //     D.Log(hit.collider.gameObject.name);
                //     if (hit.collider.gameObject != H.Arena.bombVisuals)
                //         return true;
                // }

                float defusingTime = SnDModeRules.defusingTime;

                actionsReturnClass.Actions.Add(new ActionsTypesClass
                {
                    Name = "DEFUSE",
                    Action = delegate
                    {
                        if (owner.Player.CurrentState is IdleStateClass)
                        {
                            Singleton<BombStatePacketHandler>.Instance.Send(owner.Player, BombState.Defusing, H.Arena.BombPlantedPosition);

                            owner.ShowObjectivesPanel("Defusing {0:F1}", defusingTime);
                            owner.Player.CurrentManagedState.Plant(enabled: true, false, defusingTime, (bool successful) =>
                            {
                                owner.CloseObjectivesPanel();
                                // Re-read in case another defuser already changed state
                                Vector3 pos = H.Arena.BombPlantedPosition;
                                if (!successful)
                                {
                                    // Revert state for all clients so another CT can try
                                    Singleton<BombStatePacketHandler>.Instance.Send(owner.Player, BombState.Planted, pos);
                                    return;
                                }
                                Singleton<BombStatePacketHandler>.Instance.Send(owner.Player, BombState.Defused, pos);
                                owner.ClearInteractionState();
                            });
                        }
                        else
                        {
                            owner.DisplayPreloaderUiNotification("You can't defuse while moving");
                        }
                    }
                });
            }

            __result = actionsReturnClass;
            return false;
        }

        static Item FindBombItemInPlayer(Player player)
        {
            Item[] playerInventory = player.Profile.Inventory.GetPlayerItems(EPlayerItems.InRaidItems).ToArray();
            return playerInventory.FirstOrDefault((Item nextItem) => nextItem.TemplateId == SnDModeRules.bombTemplateId);
        }

        static Vector3 GetBombPlantPosition(Player player)
        {
            if (Physics.Raycast(player.Position, Vector3.down, out RaycastHit hit, 1f, 0))
                return hit.point;
            return player.Position;
        }
    }
}

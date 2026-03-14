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

            var roundState = H.Session.roundState;

            // Allow interaction during planting phase (RoundAction) or after bomb is planted (RoundPlanted)
            if (roundState != MatchState.RoundAction && roundState != MatchState.RoundPlanted)
                return true;

            Player player = owner.Player;
            AvailableInteractionState actionsReturnClass = new AvailableInteractionState();
            Item bomb = FindBombItemInPlayer(player);

            // ── Has bomb: planting during RoundAction only ───────────────────────────
            // (Only T players receive the bomb via BombAssignment, so no explicit faction check needed)
            if (bomb != null && roundState == MatchState.RoundAction)
            {

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

                                await ItemsUtils.TryRemoveSlot(EquipmentSlot.Backpack, H.MainPlayer, false);
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
            
            // ── No bomb + bomb is planted: defusing ──────────────────────────────────
            // Guard against T players defusing their own bomb. We allow Faction.None as
            // a fallback for clients whose faction sync hasn't arrived yet (they won't
            // have the bomb, so they're effectively CT-side in that round).
            else if (bomb == null &&
                     H.MainPlayerScore?.faction != Faction.T &&
                     (H.Session.bombState == BombState.Planted || H.Session.bombState == BombState.Defusing) &&
                     Vector3.Distance(player.Position, Singleton<ArenaController>.Instance.BombPlantedPosition) <= SnDModeRules.defuseRadius)
            {
                float defusingTime = SnDModeRules.defusingTime;

                actionsReturnClass.Actions.Add(new ActionsTypesClass
                {
                    Name = "DEFUSE",
                    Action = delegate
                    {
                        if (owner.Player.CurrentState is IdleStateClass)
                        {
                            Vector3 bombPos = Singleton<ArenaController>.Instance.BombPlantedPosition;
                            Singleton<BombStatePacketHandler>.Instance.Send(owner.Player, BombState.Defusing, bombPos);

                            owner.ShowObjectivesPanel("Defusing {0:F1}", defusingTime);
                            owner.Player.CurrentManagedState.Plant(enabled: true, false, defusingTime, (bool successful) =>
                            {
                                owner.CloseObjectivesPanel();
                                // Re-read in case another defuser already changed state
                                Vector3 pos = Singleton<ArenaController>.Instance.BombPlantedPosition;
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
            if (Physics.Raycast(player.Position, Vector3.down, out RaycastHit hit, 1f, 1 << 18))
                return hit.point;
            return player.Position;
        }
    }
}

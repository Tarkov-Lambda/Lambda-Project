using EFT;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;
using ifp.arena.shared;

using EFT.Interactive;
using EFT.InventoryLogic;
using System.Linq;
using ifp.arena.bep.Core.Gamemode;
using Comfort.Common;
using ifp.arena.bep.networking;
using UnityEngine;
using ifp.arena.bep.Core;
using ifp.arena.bep.GameTypes;
using EFT.SynchronizableObjects;
using ifp.arena.bep.Core.FX;
using ifp.arena.bep.Core.Dying;

namespace ifp.arena.bep.Patches.Tarkov
{
    internal class Patch_InteractionContextHelper_GetAvailableActions_IInteractive : ModulePatch
    {
        protected override MethodBase GetTargetMethod() =>
        AccessTools.Method(typeof(InteractionContextHelper), nameof(InteractionContextHelper.GetAvailableActions), [typeof(GamePlayerOwner), typeof(IInteractive)]);

        [PatchPrefix]
        private static bool PatchPrefix(InteractionContextHelper __instance, ref ActionsReturnClass __result, GamePlayerOwner owner, IInteractive interactive)
        {
            if (interactive == null) return true;

            if (interactive is Corpse corpse)
            {
                if (corpse.PlayerProfileID == H.MainPlayer.ProfileId) return false;
            }

            var roundState = H.Session.matchState;
            if (roundState != MatchState.RoundAction && roundState != MatchState.RoundPlanted) return true;

            Player player = owner.Player;
            AvailableInteractionState actionsReturnClass = new AvailableInteractionState();

            if (interactive is bombasik)
            {
                if (H.Session.matchState is not MatchState.RoundPlanted) return true;
                if (H.MainPlayer.IsInPronePose) return true;
                float defusingTime = SND_ModeRules.defusingTime;

                actionsReturnClass.Actions.Add(new ActionsTypesClass
                {
                    Name = "DEFUSE",
                    Action = delegate
                    {
                        if (H.Session.matchState is not MatchState.RoundPlanted) return;
                        if (owner.Player.CurrentState is IdleStateClass)
                        {
                            Singleton<BombStatePacketHandler>.Instance.Send(owner.Player, BombState.Defusing, H.BombHandler.BombPlantedPosition);

                            owner.ShowObjectivesPanel("Defusing {0:F1}", defusingTime);
                            owner.Player.CurrentManagedState.Plant(enabled: true, false, defusingTime, async (successful) =>
                            {
                                owner.CloseObjectivesPanel();
                                // Re-read in case another defuser already changed state
                                Vector3 pos = H.BombHandler.BombPlantedPosition;
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

                __result = actionsReturnClass;
                return false;
            }

            if (interactive is BombPlantZone)
            {
                __result = actionsReturnClass;
                if (roundState != MatchState.RoundAction) return false;

                Item bomb = FindBombItemInPlayer(player);
                if (bomb == null) return false;

                float plantingTime = SND_ModeRules.platingTime;

                actionsReturnClass.Actions.Add(new ActionsTypesClass
                {
                    Name = "PLANT",
                    Action = delegate
                    {
                        if (owner.Player.CurrentState is IdleStateClass)
                        {
                            Singleton<BombStatePacketHandler>.Instance.Send(owner.Player, BombState.Planting, GetBombPlantPosition(player));

                            owner.ShowObjectivesPanel("Planting {0:F1}", plantingTime);
                            owner.Player.CurrentManagedState.Plant(enabled: true, false, plantingTime, async (successful) =>
                            {
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

                __result = actionsReturnClass;
                return false;
            }

            return true;
        }


        static Item FindBombItemInPlayer(Player player)
        {
            Item[] playerInventory = player.Profile.Inventory.GetPlayerItems(EPlayerItems.InRaidItems).ToArray();
            return playerInventory.FirstOrDefault((Item nextItem) => nextItem.TemplateId == SND_ModeRules.bombTemplateId);
        }

        static Vector3 GetBombPlantPosition(Player player)
        {
            if (Physics.Raycast(player.Position, Vector3.down, out RaycastHit hit, 1f, 0))
                return hit.point;
            return player.Position;
        }
    }

}

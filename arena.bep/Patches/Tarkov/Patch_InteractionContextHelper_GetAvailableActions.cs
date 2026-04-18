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
using ifp.arena.bep.GameTypes;
using ifp.arena.bep.Core.FX;
using ifp.arena.bep.Core;

namespace ifp.arena.bep.Patches.Tarkov;

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

            bool hasDefuseKit = TryFindItem(SND_ModeRules.defuseKitTemplateId, out Item defuseKit);
            float defusingTime = hasDefuseKit ? SND_ModeRules.defusingTime / 2 : SND_ModeRules.defusingTime;

            actionsReturnClass.Actions.Add(new ActionsTypesClass
            {
                Name = "DEFUSE",
                Action = delegate
                {
                    if (H.Session.matchState is not MatchState.RoundPlanted) return;
                    if (owner.Player.CurrentState is IdleStateClass)
                    {
                        Singleton<BombStatePacketHandler>.Instance.Send(H.MainPlayer, BombState.Defusing, H.BombHandler.BombPlantedPosition);

                        owner.ShowObjectivesPanel("Defusing {0:F1}", defusingTime);
                        owner.Player.CurrentManagedState.Plant(enabled: true, false, defusingTime, async (successful) =>
                        {
                            owner.CloseObjectivesPanel();
                            // Re-read in case another defuser already changed state
                            Vector3 pos = H.BombHandler.BombPlantedPosition;
                            if (!successful)
                            {
                                // Revert state for all clients so another CT can try
                                Singleton<BombStatePacketHandler>.Instance.Send(H.MainPlayer, BombState.Planted, pos);
                                return;
                            }
                            Singleton<BombStatePacketHandler>.Instance.Send(H.MainPlayer, BombState.Defused, pos);
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
            if (roundState != MatchState.RoundAction) return true;

            if (!TryFindItem(SND_ModeRules.bombTemplateId, out Item bomb)) return true;

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

                            await H.MainPlayer.TryPopContainedItem(EquipmentSlot.Backpack, false);
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

        if (H.Arena.ActiveRules is SND_ModeRules && interactive is ObservedLootItem observedLootItem)
        {
            if (observedLootItem.TemplateId == SND_ModeRules.bombTemplateId)
            {
                if (H.MainPlayerScore.Faction != Faction.T)
                {
                    __result = actionsReturnClass;
                    return true;
                }
            }
        }

        return true;
    }


    static bool TryFindItem(string templateId, out Item item)
    {
        Item[] playerInventory = H.MainPlayer.Profile.Inventory.GetPlayerItems(EPlayerItems.InRaidItems).ToArray();
        item = playerInventory.FirstOrDefault(nextItem => nextItem.TemplateId == templateId);
        return item != null;
    }

    static Vector3 GetBombPlantPosition(Player player)
    {
        if (Physics.Raycast(player.Position, Vector3.down, out RaycastHit hit, 1f, 0))
            return hit.point;
        return player.Position;
    }
}

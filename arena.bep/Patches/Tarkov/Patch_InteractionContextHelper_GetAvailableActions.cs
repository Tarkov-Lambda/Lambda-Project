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

            Plugin.Logger.LogInfo("ENTERED BOMBA ZONE");

            AvailableInteractionState actionsReturnClass = new AvailableInteractionState();

            Item bomb = FindBombItemInPlayer(owner.Player);
            if (bomb == null)
                return false;

            Plugin.Logger.LogInfo("FOUND BOMBA IN PLAYER INVENTOYR");

            float plantingTime = SnDModeRules.platingTime;

            actionsReturnClass.Actions.Add(new ActionsTypesClass
            {
                Name = "PLANT",
                Action = delegate
                {
                    if (owner.Player.CurrentState is IdleStateClass)
                    {
                        owner.ShowObjectivesPanel("Planting {0:F1}", plantingTime);
                        owner.Player.CurrentManagedState.Plant(enabled: true, false, plantingTime, (bool successful) =>
                        {
                            owner.Player.vmethod_6(bomb.TemplateId, itemTrigger.Id, successful);
                            owner.CloseObjectivesPanel();
                            if (!successful)
                            {
                                return;
                            }
                            owner.Player.InventoryController.TryRunNetworkTransaction(InteractionsHandlerClass.Remove(bomb, owner.Player.InventoryController, simulate: true), delegate (IResult discardResult)
                            {
                                if (discardResult.Succeed)
                                {
                                    owner.ClearInteractionState();
                                }
                            });
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

            string targetTemplateId = "57347da92459774491567cf5"; // tushonka (large)

            Item resultItem = playerInventory.FirstOrDefault((Item nextItem) => nextItem.TemplateId == targetTemplateId);

            return resultItem;
        }
    }
}

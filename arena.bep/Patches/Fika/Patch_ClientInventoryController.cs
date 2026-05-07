using System.Reflection;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using Fika.Core.Main.ClientClasses;
using Fika.Core.Main.HostClasses;
using HarmonyLib;
using ifp.arena.bep.networking;
using SPT.Reflection.Patching;
using static Fika.Core.Main.ClientClasses.ClientInventoryController;

// mid way to auto-resync inventory in case of a missing item error
internal class Patch_ClientInventoryOperationHandler_ReceiveStatusFromServer : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(ClientInventoryOperationHandler), nameof(ClientInventoryOperationHandler.ReceiveStatusFromServer));

    [PatchPostfix]
    public static void Postfix(ClientInventoryOperationHandler __instance, ServerOperationStatus serverStatus)
    {
        if (serverStatus.Status == EOperationStatus.Failed && serverStatus.Error.StartsWith("Could not find item"))
        {
            InventoryController inventoryController = __instance.Operation.TraderControllerClass as InventoryController;
            if (inventoryController.Profile != null)
            {
                Singleton<InventoryResyncPacketHandler>.Instance.Send(H.GetPlayer(inventoryController.Profile.ProfileId));
            }
        }
    }
}
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

// internal class Patch_ClientInventoryOperationHandler_HandleResult : ModulePatch
// {
//     protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(HostInventoryOperationHandler), nameof(HostInventoryOperationHandler.HandleResult));

//     [PatchPostfix]
//     public static void Postfix(ClientInventoryOperationHandler __instance, ServerOperationStatus serverStatus)
//     {
//         // D.Log("ASOkdnsa");
//         if (serverStatus.Status == EOperationStatus.Failed)
//         {
//             if (serverStatus.Error.StartsWith("Could not find item"))
//             {
//                 if (__instance.Operation is RemoveOperationClass removeOperation)
//                 {
//                     removeOperation.ItemAddress_0.RemoveWithoutRestrictions(removeOperation.Item);

//                     removeOperation.ItemAddress_0.RaiseRemoveEvent(removeOperation.Item, CommandStatus.Begin, __instance.InventoryController);
//                     removeOperation.ItemAddress_0.RaiseRemoveEvent(removeOperation.Item, CommandStatus.Succeed, __instance.InventoryController);
//                 }
//                 else if (__instance.Operation is ThrowOperationClass throwOperation)
//                 {
//                     throwOperation.ItemAddress_0.RemoveWithoutRestrictions(throwOperation.Item);

//                     throwOperation.ItemAddress_0.RaiseRemoveEvent(throwOperation.Item, CommandStatus.Begin, __instance.InventoryController);
//                     throwOperation.ItemAddress_0.RaiseRemoveEvent(throwOperation.Item, CommandStatus.Succeed, __instance.InventoryController);
//                 }
//             }
//             D.Notify("Error occured, resynchronizing inventory");
//             // Singleton<InventoryResyncPacketHandler>.Instance.Send();
//         }
//     }
// }
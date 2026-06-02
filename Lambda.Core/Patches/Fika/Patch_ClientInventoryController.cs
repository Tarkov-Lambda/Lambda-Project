using System.Reflection;
using Comfort.Common;
using EFT;
using Fika.Core.Main.ClientClasses;
using HarmonyLib;
using Lambda.Core.Networking;
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
            Singleton<EquipmentResyncPacketWarden>.Instance.Send(__instance.InventoryController.FikaPlayer); // maybe this works?
        }
    }
}
using System.Reflection;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using HarmonyLib;
using Lambda.Core.Networking;
using SPT.Reflection.Patching;

internal class Patch_ClientPlayer_method_1 : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(ClientPlayer.Class2443.Class1678), nameof(ClientPlayer.Class2443.Class1678.method_1));

    [PatchPrefix]
    public static void Prefix(ClientPlayer.Class2443.Class1678 __instance, ClientPlayer.Struct555 serverState)
    {
        if (serverState.Status == EOperationStatus.Failed && serverState.Error.StartsWith("Could not find item"))
        {
            InventoryController inventoryController = __instance.operation.TraderControllerClass as InventoryController;
            if (inventoryController.Profile != null)
            {
                Singleton<InventoryResyncPacketHandler>.Instance.Send(H.GetPlayer(inventoryController.Profile.ProfileId));
            }
        }
    }
}

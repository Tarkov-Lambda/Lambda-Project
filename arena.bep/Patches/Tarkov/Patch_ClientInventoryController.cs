using System.Reflection;
using EFT;
using EFT.InventoryLogic;
using HarmonyLib;
using SPT.Reflection.Patching;

internal class Patch_ClientPlayer_method_1 : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(ClientPlayer.Class2443.Class1678), nameof(ClientPlayer.Class2443.Class1678.method_1));

    [PatchPrefix]
    public static void Prefix(ClientPlayer.Class2443.Class1678 __instance, ClientPlayer.Struct555 serverState)
    {
        D.Log("AOPKSDNPIOA");
        if (serverState.Status == EOperationStatus.Failed)
        {
            D.Log("ASDAD");
            if (serverState.Error.StartsWith("Could not find item"))
            {
                D.Log("cbabadb");
                if (__instance.operation is RemoveOperationClass removeOperation)
                {
                D.Log("AS   dwerqwfgDAD");

                    removeOperation.ItemAddress_0.RemoveWithoutRestrictions(removeOperation.Item);

                    removeOperation.ItemAddress_0.RaiseRemoveEvent(removeOperation.Item, CommandStatus.Begin, __instance.class2443_0.ClientPlayer_0.InventoryController);
                    removeOperation.ItemAddress_0.RaiseRemoveEvent(removeOperation.Item, CommandStatus.Succeed, __instance.class2443_0.ClientPlayer_0.InventoryController);
                }
                else if (__instance.operation is ThrowOperationClass throwOperation)
                {
                D.Log("AS   dwerqwfgDAD");

                    throwOperation.ItemAddress_0.RemoveWithoutRestrictions(throwOperation.Item);

                    throwOperation.ItemAddress_0.RaiseRemoveEvent(throwOperation.Item, CommandStatus.Begin, __instance.class2443_0.ClientPlayer_0.InventoryController);
                    throwOperation.ItemAddress_0.RaiseRemoveEvent(throwOperation.Item, CommandStatus.Succeed, __instance.class2443_0.ClientPlayer_0.InventoryController);
                }
            }
        }
    }
}

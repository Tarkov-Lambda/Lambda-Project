using System.Reflection;
using EFT;
using EFT.InputSystem;
using HarmonyLib;
using SPT.Reflection.Patching;

public class Patch_GamePlayerOwner_TranslateCommand : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(GamePlayerOwner), nameof(GamePlayerOwner.TranslateCommand));

    public static bool IsRessetingFreelook { get; private set; } = false;

    [PatchPrefix]
    static void Prefix(ECommand command)
    {
        if (command == ECommand.ResetLookDirection)
        {
            IsRessetingFreelook = true;
        }
    }

    [PatchPostfix]
    static void Postfix(ECommand command)
    {
        if (command == ECommand.ResetLookDirection)
        {
            IsRessetingFreelook = false;
        }
    }
}
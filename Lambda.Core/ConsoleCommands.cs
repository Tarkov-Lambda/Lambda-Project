


using System;
using Comfort.Common;
using EFT.Console.Core;
using Lambda.Core;
using Lambda.Core.Networking;

[ConsoleGroup("l")]
internal class LambdaConsoleCommands
{
    [ConsoleCommand("set_svar", "", null, "Set a Server Variable")]
    internal static void SetServerVariable([ConsoleArgument("LeanSpeed", "Variable Name")] string variableName, [ConsoleArgument("1.3f", "Variable Value")] string value)
    {
        var isSet = GameplayVariables.SetFieldValue(variableName, value);
        if (isSet)
        {
            Singleton<GameplayVariablesSyncPacketWarden>.Instance.Send();
        }
    }

    [ConsoleCommand("list_svars", "", null, "List all available gamemodes")]
    internal static void ListServerVariables()
    {
        var fieldDumps = GameplayVariables.GetAllFieldStrings();

        foreach (var fieldDump in fieldDumps)
        {
            Plugin.Logger.LogInfo(fieldDump);
        }
    }
}



using System;
using Comfort.Common;
using EFT.Console.Core;
using Lambda.Core;
using Lambda.Core.Networking;

[ConsoleGroup("l")]
internal class LambdaConsoleCommands
{

    [ConsoleCommand("changefaction", "", null, "List all available factions")]
    internal static void ChangeFaction([ConsoleArgument("CT", "Which faction to switch to")] string faction)
    {

    }

    [ConsoleCommand("listfaction", "", null, "List all available factions")]
    internal static void ListFactions([ConsoleArgument("CT", "Which faction to switch to")] string faction)
    {

    }

    [ConsoleCommand("setpassword", "", null, "set admin password")]
    internal static void ChangeAdminPassword([ConsoleArgument("", "Adming password")] string password)
    {
        Singleton<AdminLoginPacketWarden>.Instance.Send();
    }

    [ConsoleCommand("login", "", null, "Admin login")]
    internal static void AdminLogin()
    {
        Singleton<AdminLoginPacketWarden>.Instance.Send();
    }

    [ConsoleCommand("changelevel", "", null, "Admin only: Change server's map")]
    internal static void ChangeMap([ConsoleArgument("", "Desired map")] string level)
    {

    }

    [ConsoleCommand("listlevel", "", null, "List available maps")]
    internal static void ListMaps()
    {

    }

    [ConsoleCommand("changegamemode", "", null, "Admin only: Change Gamemode")]
    internal static void ChangeGamemode([ConsoleArgument("", "Desired gamemode")] string level)
    {

    }

    [ConsoleCommand("listgamemodes", "", null, "List all available gamemodes")]
    internal static void ListGamemodes()
    {

    }

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

    [ConsoleCommand("startsession", "", null, "Admin only: Start session")]
    internal static void StartSession()
    {
        Singleton<SessionStartPacketWarden>.Instance.Send();
    }

    [ConsoleCommand("endsession", "", null, "Admin only: End session")]
    internal static void StopSession()
    {
        Singleton<SessionStopPacketWarden>.Instance.Send();
    }
}
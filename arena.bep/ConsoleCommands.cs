


using System;
using Comfort.Common;
using EFT.Console.Core;
using ifp.arena.bep.networking;

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
        Singleton<AdminLoginPacketHandler>.Instance.Send();
    }

    [ConsoleCommand("login", "", null, "Admin login")]
    internal static void AdminLogin()
    {
        Singleton<AdminLoginPacketHandler>.Instance.Send();
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

    [ConsoleCommand("set_svar", "", null, "List all available gamemodes")]
    internal static void SetServerVariable()
    {

    }

    [ConsoleCommand("startsession", "", null, "Admin only: Start session")]
    internal static void StartSession()
    {
        Singleton<SessionStartPacketHandler>.Instance.Send();
    }

    [ConsoleCommand("endsession", "", null, "Admin only: End session")]
    internal static void StopSession()
    {
        Singleton<SessionStopPacketHandler>.Instance.Send();
    }
}
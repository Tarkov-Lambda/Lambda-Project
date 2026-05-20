using Comfort.Common;
using Lambda.Core;
using Lambda.Core.Networking;
using Lambda.Core.Networking.Commands;
using PacketWarden;

public static class GeneralPlayerCommands
{
    [ChatCommand("login", "Requests server login", CommandTarget.ClientOnly, PacketAuthority.Anyone)]
    public static void Login(CommandContext ctx, string password)
    {
        Plugin.Password.Value = password;
        Singleton<AdminLoginPacketWarden>.Instance.Send();
    }

    [ChatCommand("pause", "Requests server login", CommandTarget.ClientOnly, PacketAuthority.Anyone)]
    public static void Pause(CommandContext ctx, string password)
    {
        Singleton<SessionPausePacketWarden>.Instance.Send();
    }

    [ChatCommand("setclan", "Set your new clan tag", CommandTarget.ClientOnly, PacketAuthority.Anyone)]
    public static void SetClanTag(CommandContext ctx, string clanTag)
    {
        Singleton<ClanTagResyncPacketWarden>.Instance.Send(clanTag);
    }
}
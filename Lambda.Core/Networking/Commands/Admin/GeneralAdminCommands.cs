using Comfort.Common;
using EFT;
using Lambda.Core.Networking;
using Lambda.Core.Networking.Commands;
using PacketWarden;

public static class AdminCommands
{
    [ChatCommand("map", "Starts the current session.", CommandTarget.ServerOnly, PacketAuthority.Admin)]
    public static void StartMatchCommand(CommandContext ctx, string mapname)
    {
        H.Session.level = mapname;

        Singleton<AnnouncementPacketWarden>.Instance.Send($"Changed next map to {mapname}");
    }

    [ChatCommand("start", "Starts the current session.", CommandTarget.ServerOnly, PacketAuthority.Admin)]
    public static void StartMatchCommand(CommandContext ctx)
    {
        Singleton<SessionStartPacketWarden>.Instance.Send();
        Singleton<AnnouncementPacketWarden>.Instance.Send($"Session is starting.");
    }

    [ChatCommand("stop", "Ends the current session.", CommandTarget.ServerOnly, PacketAuthority.Admin)]
    public static void StopMatchCommand(CommandContext ctx)
    {
        Singleton<SessionStopPacketWarden>.Instance.Send();
        Singleton<AnnouncementPacketWarden>.Instance.Send($"An admin has stopped the match.");
    }

    [ChatCommand("givemoney", "Gives you money", CommandTarget.ServerOnly, PacketAuthority.Admin)]
    public static void GiveMoneyCommand(CommandContext ctx, Player player, int amount)
    {
        var pContext = player.GetContext();
        pContext.AddMoney(amount);
        Singleton<MoneyResyncPacketWarden>.Instance.Send(player, pContext.Money);
        Singleton<AnnouncementPacketWarden>.Instance.SendToPlayer(player, $"You've received {amount / 80}");
    }

    [ChatCommand("kick", "Kicks a player", CommandTarget.ServerOnly, PacketAuthority.Admin)]
    public static void KickCommand(CommandContext ctx, Player target, string reason)
    {
        var peerId = PacketWardenUtils.Network.GetPeerIdByPlayer(target);
        PacketWardenUtils.Network.DisconnectPeer(peerId);
        Singleton<AnnouncementPacketWarden>.Instance.Send($"{target.Profile.Nickname} was kicked. Reason: {reason}");
    }
}

public static class ClientCommands
{
    // Usage: !clear
    // Target is ClientOnly, so this never hits the server.
    [ChatCommand("clear", "Clears local chat", CommandTarget.ClientOnly, PacketAuthority.Anyone)]
    public static void ClearChatCommand(CommandContext ctx)
    {

        ctx.Reply("Chat cleared locally.");
    }
}
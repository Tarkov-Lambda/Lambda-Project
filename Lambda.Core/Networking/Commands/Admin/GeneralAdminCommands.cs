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
        ctx.Reply($"Changed next map to {mapname}");
    }

    [ChatCommand("start", "Starts the current session.", CommandTarget.ServerOnly, PacketAuthority.Admin)]
    public static void StartMatchCommand(CommandContext ctx)
    {
        Singleton<SessionStartPacketWarden>.Instance.Send();
        ctx.Reply("Session is starting.");
    }

    [ChatCommand("stop", "Ends the current session.", CommandTarget.ServerOnly, PacketAuthority.Admin)]
    public static void StopMatchCommand(CommandContext ctx)
    {
        Singleton<SessionStopPacketWarden>.Instance.Send();
        ctx.Reply("An admin has stopped the match.");
    }

    [ChatCommand("givemoney", "Gives you money (in USD, but the conversion rate is favouring roubles)", CommandTarget.ServerOnly, PacketAuthority.Admin)]
    public static void GiveMoneyCommand(CommandContext ctx, Player player, int amount)
    {
        H.GetPlayerContext(player).AddMoney(amount);
        ctx.Reply($"Added {amount} money to your account.");
    }

    [ChatCommand("kick", "Kicks a player", CommandTarget.ServerOnly, PacketAuthority.Admin)]
    public static void KickCommand(CommandContext ctx, Player target, string reason)
    {
        var peerId = PacketWardenUtils.Network.GetPeerIdByPlayer(target);
        PacketWardenUtils.Network.DisconnectPeer(peerId);
        ctx.Reply($"{target.Profile.Nickname} was kicked. Reason: {reason}");
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
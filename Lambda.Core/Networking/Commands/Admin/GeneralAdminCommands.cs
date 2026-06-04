using Comfort.Common;
using EFT;
using Lambda.Core;
using Lambda.Core.Networking;
using Lambda.Core.Networking.Commands;
using PacketWarden;

public static class AdminCommands
{
    [ChatCommand("gamemode", "Starts the current session.", CommandTarget.ServerOnly, PacketAuthority.Admin)]
    public static void ChangeGamemodeCommand(CommandContext ctx, string gamemode)
    {
        LambdaPlugin.Gamemode.Value = gamemode;
        ctx.Announce($"Changed next gamemode to {gamemode}");
    }

    [ChatCommand("map", "Starts the current session.", CommandTarget.ServerOnly, PacketAuthority.Admin)]
    public static void ChangeLevelCommand(CommandContext ctx, string gamemode)
    {
        LambdaPlugin.Level.Value = gamemode;
        ctx.Announce($"Changed next map to {gamemode}");
    }

    [ChatCommand("start", "Starts the current session.", CommandTarget.ServerOnly, PacketAuthority.Admin)]
    public static void StartMatchCommand(CommandContext ctx)
    {
        Singleton<SessionStartPacketWarden>.Instance.Send();
        ctx.Announce($"Session is starting.");
    }

    [ChatCommand("stop", "Ends the current session.", CommandTarget.ServerOnly, PacketAuthority.Admin)]
    public static void StopMatchCommand(CommandContext ctx)
    {
        Singleton<SessionStopPacketWarden>.Instance.Send();
        ctx.Announce($"An admin has stopped the match.");
    }

    [ChatCommand("givemoney", "Gives you money", CommandTarget.ServerOnly, PacketAuthority.Admin)]
    public static void GiveMoneyCommand(CommandContext ctx, Player player, int amount)
    {
        var pContext = player.Context;
        pContext.AddMoney(amount);
        Singleton<MoneyResyncPacketWarden>.Instance.Send(player, pContext.Money);
        ctx.Reply($"You've received {amount / 80}");
    }

    [ChatCommand("kick", "Kicks a player", CommandTarget.ServerOnly, PacketAuthority.Admin)]
    public static void KickCommand(CommandContext ctx, Player target, string reason)
    {
        var peerId = PacketWardenUtils.Network.GetPeerIdByPlayer(target);
        PacketWardenUtils.Network.DisconnectPeer(peerId);
        ctx.Announce($"{target.Profile.Nickname} was kicked. Reason: {reason}");
    }

    [ChatCommand("kill", "kill a player", CommandTarget.ServerOnly, PacketAuthority.Admin)]
    public static void KillCommand(CommandContext ctx, Player player)
    {
        var damageInfo = new DamageInfoStruct
        {
            Damage = 1f,
            BodyPartColliderType = EBodyPartColliderType.RibcageUp
        };
        Singleton<PlayerKilledPacketWarden>.Instance.Send(damageInfo, player, player);
    }

    [ChatCommand("svar_set", "synchronize all variables", CommandTarget.ClientOnly, PacketAuthority.Admin)]
    public static void GameplayVariableResynchronization(CommandContext ctx, string svar_name, string svar_value)
    {
        var success = GameplayVariables.SetFieldValue(svar_name, svar_value);
        if (success)
        {
            Singleton<GameplayVariablesSyncPacketWarden>.Instance.Send();
        }
    }
}
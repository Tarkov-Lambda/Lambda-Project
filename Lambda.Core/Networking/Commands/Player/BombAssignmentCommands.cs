using Comfort.Common;
using EFT;
using Lambda.Core.Networking;
using Lambda.Core.Networking.Commands;
using PacketWarden;

public static class BombAssignmentCommands
{
    [ChatCommand("getbomb", "Requests the server to assign them as the new bomb carrier", CommandTarget.ClientOnly, PacketAuthority.Anyone)]
    public static void GetBomb(CommandContext ctx)
    {
        Singleton<AskForBombPriorityPacketWarden>.Instance.Send(true);
    }

    [ChatCommand("cancelbomb", "Requests the server to assign them as the new bomb carrier", CommandTarget.ClientOnly, PacketAuthority.Anyone)]
    public static void CancelBomb(CommandContext ctx)
    {
        Singleton<AskForBombPriorityPacketWarden>.Instance.Send(false);
    }
}
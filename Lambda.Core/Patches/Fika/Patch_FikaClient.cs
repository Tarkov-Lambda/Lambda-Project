using Fika.Core.Main.Components;
using Fika.Core.Networking;
using Fika.Core.Networking.Packets.Player.Common;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;

namespace Lambda.Core.Patches;

internal class Patch_FikaClient_OnCommonPlayerPacketReceived : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(FikaClient), "OnCommonPlayerPacketReceived");

    [PatchPrefix]
    static bool Prefix(CoopHandler ____coopHandler, CommonPlayerPacket packet)
    {
        D.Notify(packet.SubPacket.ToString());
        // if (____coopHandler.Players.TryGetValue(packet.NetId, out var playerToApply))
        // {
        //     if (packet.Type == ECommonSubPacketType.Damage)
        //     {
        //         Plugin.Logger.LogInfo(packet.SubPacket);
        //     }
        //     packet.Execute(playerToApply);
        // }

        return true;
    }
}

using Fika.Core.Main.Components;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.Packets.Player.Common;
using HarmonyLib;
using ifp.arena.bep.Core;
using SPT.Reflection.Patching;
using System.Reflection;

namespace ifp.arena.bep.Patches.Fika
{
    internal class Patch_FikaClient_OnCommonPlayerPacketReceived : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(FikaClient), "OnCommonPlayerPacketReceived");
        }

        
        [PatchPrefix]
        static bool Prefix(CoopHandler ____coopHandler, CommonPlayerPacket packet)
        {
            if (!Plugin.Active.Value) return true;
            H.Log(packet.Type.ToString());

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
}

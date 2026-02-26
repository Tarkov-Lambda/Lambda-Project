using Fika.Core.Main.Components;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.Packets.Player.Common;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;

namespace ifp.arena.bep
{
    internal class Patch_Fika_OnCommonPlayerPacketReceived : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(Fika.Core.Networking.FikaServer), "OnCommonPlayerPacketReceived");
        }

        
        [PatchPrefix]
        static bool Prefix(CoopHandler ____coopHandler, CommonPlayerPacket packet, NetPeer peer)
        {
            if (!Plugin.Active.Value) return true;

            if (____coopHandler.Players.TryGetValue(packet.NetId, out var value))
            {
                if (packet.Type == ECommonSubPacketType.Damage)
                {
                    Plugin.Logger.LogInfo(packet.SubPacket);
                }
                packet.Execute(value);


            }

            return false;
        }
    }
}

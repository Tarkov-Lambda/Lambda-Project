using Fika.Core.Main.Components;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.Packets.Player.Common;
using HarmonyLib;
using ifp.arena.bep.networking.TimeSync;
using SPT.Reflection.Patching;
using System.Reflection;

namespace ifp.arena.bep.Patches.Fika
{
    internal class Patch_FikaClientOnNetworkLatencyUpdate : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(FikaClient), nameof(FikaClient.OnNetworkLatencyUpdate));
        }

        
        [PatchPostfix]
        static void Postfix(NetPeer peer)
        {
            if (peer == null)
                return;

            ServerUtcClock.UpdateFromPeer(peer);
        }
    }
}

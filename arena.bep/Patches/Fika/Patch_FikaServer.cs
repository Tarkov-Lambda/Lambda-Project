using Comfort.Common;
using EFT;
using Fika.Core.Main.Components;
using Fika.Core.Main.Players;
using Fika.Core.Main.Utils;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.Packets.Player.Common;
using Fika.Core.Networking.Packets.Player.Common.SubPackets;
using HarmonyLib;
using ifp.arena.bep.Core;
using ifp.arena.bep.networking;
using SPT.Reflection.Patching;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace ifp.arena.bep.Patches
{
    internal sealed class Patch_OnCommonPlayerPacketReceived : ModulePatch
    {
        private static readonly AccessTools.FieldRef<FikaServer, CoopHandler> CoopHandlerRef =
            AccessTools.FieldRefAccess<FikaServer, CoopHandler>("_coopHandler");

        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(FikaServer), "OnCommonPlayerPacketReceived");
        }

        [PatchPrefix]
        private static bool Prefix(FikaServer __instance, CommonPlayerPacket packet, NetPeer peer)
        {
            // Only run this logic on the server runtime
            if (!FikaBackendUtils.IsServer)
                return true;

            if (packet?.SubPacket == null)
                return true;

            if (packet.Type != ECommonSubPacketType.Damage)
                return true;

            if (packet.SubPacket is not DamagePacket damage)
                return true;

            var coopHandler = CoopHandlerRef(__instance);
            if (coopHandler?.Players == null)
                return true;

            // DamagePacket.NetId is the VICTIM netId in Fika
            if (!coopHandler.Players.TryGetValue(damage.NetId, out var victim) || victim == null)
                return false;


            var wasAlive = victim.HealthController?.IsAlive == true;

            // Apply damage to the victim on the server.
            victim.HandleDamagePacket(damage);

            var isAliveNow = victim.HealthController?.IsAlive == true;

            if (wasAlive && !isAliveNow)
            {

                // Resolve killerId in terms of EFT.Player.Id (what your scoreboard lookup uses)
                var killerId = 0;

                try
                {
                    if (damage.ProfileId.HasValue)
                    {
                        // Mirrors FikaPlayer.HandleDamagePacket logic
                        var killerBridge = H.GameWorld.GetAlivePlayerBridgeByProfileID(damage.ProfileId.Value);
                        if (killerBridge?.iPlayer is Player killerPlayer)
                        {
                            killerId = killerPlayer.Id;
                        }
                        else if (killerBridge?.iPlayer is FikaPlayer killerFika)
                        {
                            killerId = killerFika.Id;
                        }
                    }
                }
                catch
                {
                    // fallback to 0
                }

                
                
                // Singleton<PlayerKilledPacketHandler>.Instance.Send(killerId, victim.Id, assistId, true);
            }

            return false;
        }
    }
}

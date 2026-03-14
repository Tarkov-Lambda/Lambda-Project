using Comfort.Common;
using EFT;
using Fika.Core.Main.Components;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.Packets.Player.Common;
using Fika.Core.Networking.Packets.Player.Common.SubPackets;
using HarmonyLib;
using ifp.arena.bep.Core;
using ifp.arena.bep.networking;
using SPT.Reflection.Patching;
using System.Reflection;

namespace ifp.arena.bep.Patches
{
    internal sealed class Patch_FikaServer_OnCommonPlayerPacketReceived : ModulePatch
    {
        private static readonly AccessTools.FieldRef<FikaServer, CoopHandler> CoopHandlerRef = AccessTools.FieldRefAccess<FikaServer, CoopHandler>("_coopHandler");

        protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(FikaServer), "OnCommonPlayerPacketReceived");

        [PatchPostfix]
        private static void Postfix(FikaServer __instance, CommonPlayerPacket packet, NetPeer peer)
        {
            if (packet.Type != ECommonSubPacketType.Damage) return;
            if (packet.SubPacket is not DamagePacket damage) return;

            var coopHandler = CoopHandlerRef(__instance);
            int victimNetId = packet.NetId;

            if (!coopHandler.Players.TryGetValue(victimNetId, out var victim)) return;

            // we handle the server owner player in the Patch_Kill
            // kind of ass backwards, but it makes sense in my head rn
            if (victim.IsYourPlayer) return;

            // Instead of waiting for healthsync, we apply a damage packet directly on the server on a player that's not ours.
            // I can't vouch as per how accurate this is going to be
            // but in theory this should be just fine, and if the client heals, they will send a healthsync packet later
            //
            // The only catch can be if the client sends a healthsync of their regened health right after we send this packet
            // shooter sends a packet of 60 damage to thorax of victim
            // victim sends a sync packet saying they just healed, right after we just send them a damage packet.
            // eventually the victim will be the source of truth when we healthsync, but just how serious is sync mismatch here given low ttk?
            // although, at the end of the day the other player will eventually pick up all the damage packets and apply them, in worst case scenario dying themselves (right?)
            victim.HandleDamagePacket(damage);

            if (H.Scoreboard[victim.Id].isAlive == false) return;
            H.Log(victim.Profile.Nickname);

            // Check if head or chest is blacked out after this damage
            var headHP = victim.ActiveHealthController.GetBodyPartHealth(EBodyPart.Head, false);
            var chestHP = victim.ActiveHealthController.GetBodyPartHealth(EBodyPart.Chest, false);

            if (headHP.AtMinimum || chestHP.AtMinimum)
            {
                Singleton<PlayerKilledPacketHandler>.Instance.Send(damage);
            }
        }
    }
}

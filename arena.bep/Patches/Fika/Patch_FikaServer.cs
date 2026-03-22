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
using System.Reflection;

namespace ifp.arena.bep.Patches
{

    // [Info   : arena.bep] CoriumOperator
    // [Error  : Unity Log] NullReferenceException: Object reference not set to an instance of an object
    // Stack trace:
    // ifp.arena.bep.Patches.Patch_FikaServer_OnCommonPlayerPacketReceived.Postfix (Fika.Core.Networking.FikaServer __instance, Fika.Core.Networking.Packets.Player.Common.CommonPlayerPacket packet, Fika.Core.Networking.LiteNetLib.NetPeer peer) (at <00a04958e6b5425baf50a72d6d850112>:0)
    // (wrapper dynamic-method) Fika.Core.Networking.FikaServer.DMD<Fika.Core.Networking.FikaServer::OnCommonPlayerPacketReceived>(Fika.Core.Networking.FikaServer,Fika.Core.Networking.Packets.Player.Common.CommonPlayerPacket,Fika.Core.Networking.LiteNetLib.NetPeer)
    // Fika.Core.Networking.LiteNetLib.Utils.NetPacketProcessor+<>c__DisplayClass27_0`2[T,TUserData].<SubscribeNetReusable>b__0 (Fika.Core.Networking.LiteNetLib.Utils.NetDataReader reader, System.Object userData) (at <4961a269c1a0469488965fa870906146>:0)
    // Fika.Core.Networking.LiteNetLib.Utils.NetPacketProcessor.ReadPacket (Fika.Core.Networking.LiteNetLib.Utils.NetDataReader reader, System.Object userData) (at <4961a269c1a0469488965fa870906146>:0)
    // Fika.Core.Networking.LiteNetLib.Utils.NetPacketProcessor.ReadAllPackets (Fika.Core.Networking.LiteNetLib.Utils.NetDataReader reader, System.Object userData) (at <4961a269c1a0469488965fa870906146>:0)
    // Fika.Core.Networking.FikaServer.OnNetworkReceive (Fika.Core.Networking.LiteNetLib.NetPeer peer, Fika.Core.Networking.LiteNetLib.NetPacketReader reader, System.Byte channelNumber, Fika.Core.Networking.LiteNetLib.DeliveryMethod deliveryMethod) (at <4961a269c1a0469488965fa870906146>:0)
    // Fika.Core.Networking.LiteNetLib.NetManager.ProcessEvent (Fika.Core.Networking.LiteNetLib.NetEvent evt) (at <4961a269c1a0469488965fa870906146>:0)
    // Fika.Core.Networking.LiteNetLib.LiteNetManager.PollEvents () (at <4961a269c1a0469488965fa870906146>:0)
    // Fika.Core.Networking.FikaServer.Update () (at <4961a269c1a0469488965fa870906146>:0)
    // UnityEngine.DebugLogHandler:LogException(Exception, Object)
    // Class412:LogException(Exception, Object)
    // UnityEngine.Debug:CallOverridenDebugHandler(Exception, Object)

    internal sealed class Patch_FikaServer_OnCommonPlayerPacketReceived : ModulePatch
    {
        private static readonly AccessTools.FieldRef<FikaServer, CoopHandler> CoopHandlerRef = AccessTools.FieldRefAccess<FikaServer, CoopHandler>("_coopHandler");

        protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(FikaServer), "OnCommonPlayerPacketReceived");

        [PatchPostfix]
        private static void Postfix(FikaServer __instance, CommonPlayerPacket packet, NetPeer peer)
        {
            // D.Log($"{peer.Id} sent {packet.GetType()} {packet.Type}");
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
            // although, at the end of the day the victim will eventually pick up all the damage packets and apply them, in worst case scenario killing themselves (right?)
            victim.HandleDamagePacket(damage);

            if (H.Scoreboard[victim.Id].isAlive == false) return;
            D.Log(victim.Profile.Nickname);

            // Check if head or chest is blacked out after this damage
            // D.Dump(victim);
            // D.Dump(victim.HealthController);

            var headHP = victim.HealthController.GetBodyPartHealth(EBodyPart.Head, false);
            var chestHP = victim.HealthController.GetBodyPartHealth(EBodyPart.Chest, false);

            if (headHP.AtMinimum || chestHP.AtMinimum)
            {
                D.Log("Died");
                Singleton<PlayerKilledPacketHandler>.Instance.Send(damage);
            }
        }
    }
}

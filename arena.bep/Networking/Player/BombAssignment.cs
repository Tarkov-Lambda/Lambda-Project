using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.Core;
using ifp.arena.bep.Core.Gamemode;
using ifp.arena.bep.GameTypes;
using ifp.arena.bep.networking.Base;
using ifp.arena.bep.networking.TimeSync;
using MemoryPack;
using System.Linq;
using UnityEngine;

namespace ifp.arena.bep.networking
{
    [MemoryPackable]
    public partial struct BombAssignmentPacket : INetSerializable
    {
        public int playerId;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(playerId);
        }

        public void Deserialize(NetDataReader reader)
        {
            playerId = reader.GetInt();
        }

    }

    public class BombAssignmentPacketHandler : PacketHandler<BombStatePacket>
    {
        public BombAssignmentPacketHandler() : base(DeliveryMethod.ReliableOrdered, PacketAuthority.ServerOnly) { }

        public void Send()
        {
            var packet = new BombStatePacket
            {
                playerId = H.Session.GetPlayersFromFaction(shared.Faction.T).RandomElement().Id,
            };
            RequestSendToPlayer(packet, packet.playerId);
        }

        // P.S this is extremely bad practice and I need to refactor item spawning to be less trustful
        public override void WhenApproved(BombStatePacket packet, NetPeer peer)
        {
            Item BombBackpack = ItemsUtils.CreateItemFromTemplateId(SnDModeRules.bombTemplateId);
            _ = ItemsUtils.ClientRequestGiveItem(BombBackpack);
        }
    }
}
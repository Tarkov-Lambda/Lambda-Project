using Comfort.Common;
using Cysharp.Threading.Tasks;
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

    public class BombAssignmentPacketHandler : PacketHandler<BombAssignmentPacket>
    {
        public BombAssignmentPacketHandler() : base(DeliveryMethod.ReliableOrdered, PacketAuthority.ServerOnly) { }

        public void Send()
        {
            if (H.Session.GetPlayersFromFaction(shared.Faction.T).Count > 0)
            {
                var randomTerrorist = H.Session.GetPlayersFromFaction(shared.Faction.T).RandomElement();

                var packet = new BombAssignmentPacket
                {
                    playerId = randomTerrorist.Id,
                };

                RequestSendToPlayer(packet, packet.playerId);
            }
        }

        public async UniTaskVoid SendDelayed(int delayMs = 50)
        {
            if (!Fika.Core.Main.Utils.FikaBackendUtils.IsServer) return;
            await UniTask.Delay(delayMs);
            Send();
        }

        // P.S this is extremely bad practice and I need to refactor item spawning to be less trustful
        public override void WhenApproved(BombAssignmentPacket packet, NetPeer peer)
        {
            Item BombBackpack = ItemsUtils.CreateItemFromTemplateId(SnDModeRules.bombTemplateId);
            _ = ItemsUtils.ClientRequestGiveItem(BombBackpack);
            // H.Notify("You now have the bomb");
        }
    }
}
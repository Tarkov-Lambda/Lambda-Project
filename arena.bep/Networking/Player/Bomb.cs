using Comfort.Common;
using EFT;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.Core;
using ifp.arena.bep.Core.Gamemode;
using ifp.arena.bep.GameTypes;
using ifp.arena.bep.networking.Base;
using ifp.arena.bep.networking.TimeSync;
using System.Linq;
using UnityEngine;

namespace ifp.arena.bep.networking
{
    public struct BombStatePacket : INetSerializable
    {
        public int playerId;
        public BombState state;
        public Vector3 position;
        public double timestamp;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(playerId);
            writer.Put((int)state);
            writer.Put(position);
            writer.Put(timestamp);
        }

        public void Deserialize(NetDataReader reader)
        {
            playerId = reader.GetInt();
            state = (BombState)reader.GetInt();
            position = reader.GetVector3();
            timestamp = reader.GetDouble();
        }

        public override string ToString()
        {
            return $"{playerId} state: {state}";
        }
    }

    public class BombStatePacketHandler : PacketHandler<BombStatePacket>
    {
        public void Send(Player player, BombState state, Vector3 position)
        {
            var packet = new BombStatePacket
            {
                playerId = player.Id,
                state = state,
                position = position,
                timestamp = NetworkTime.ServerNowSeconds
            };
            // H.Notify(state);

            RequestSend(packet);
        }

        public override bool ServerValidation(ref BombStatePacket packet, NetPeer peer)
        {
            // Only server is allowed to send these states
            // if (packet.state is BombState.Planted or BombState.Defused or BombState.Exploded)
            // {
            //     return false;
            // }

            return base.ServerValidation(ref packet, peer);
        }

        public override void WhenApproved(BombStatePacket packet, NetPeer peer)
        {
            H.Session.bombState = packet.state;

            if (packet.state == BombState.Planted)
            {
                //  ..literally pizdec().Invoke();

                H.Arena.LastObjectivePlayerId = packet.playerId;
            }
            H.Notify(packet.state);

            if (packet.state is BombState.Defused or BombState.Exploded)
            {
                H.Arena.LastObjectiveBombState = packet.state;
                if (packet.playerId > 0)
                    H.Arena.LastObjectivePlayerId = packet.playerId;
            }

            Singleton<ArenaController>.Instance.SetBombVisuals(packet);

            EventBus.OnBombStateChange(packet.state);
        }
    }
}
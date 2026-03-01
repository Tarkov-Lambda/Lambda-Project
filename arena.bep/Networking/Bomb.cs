using Comfort.Common;
using EFT;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.GameTypes;
using ifp.arena.bep.Networking.Base;
using ifp.arena.bep.Networking.TimeSync;
using System.Linq;

namespace ifp.arena.bep.Networking
{
    public struct BombStatePacket : INetSerializable
    {
        public int playerId;
        public BombState state;
        public double timestamp;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(playerId);
            writer.Put((int)state);
            writer.Put(timestamp);
        }

        public void Deserialize(NetDataReader reader)
        {
            playerId = reader.GetInt();
            state = (BombState)reader.GetInt();
        }

        public override string ToString()
        {
            return $"{playerId} state: {state}";
        }
    }

    public class BombStatePacketHandler : PacketHandler<BombStatePacket>
    {
        private const float PlantDuration = 7.0f;
        private const float DefuseDuration = 7.0f;

        public void Send(int playerId, BombState state)
        {
            var packet = new BombStatePacket
            {
                playerId = playerId,
                state = state,
                timestamp = 0d
            };

            RequestSend(packet);
        }

        public override bool ServerValidation(ref BombStatePacket packet, NetPeer peer)
        {
            // Only server sends these states
            if (packet.state == BombState.Planted || packet.state == BombState.Defused || packet.state == BombState.Exploded)
            {
                return false;
            }

            return base.ServerValidation(ref packet, peer);
        }

        public override void OnReceive(BombStatePacket packet, NetPeer peer)
        {
            Singleton<BaseGameMode>.Instance.session.bombState = packet.state;   
        }
    }
}
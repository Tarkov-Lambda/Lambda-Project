using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.GameTypes;
using ifp.arena.bep.Networking.Base;

namespace ifp.arena.bep.Networking
{
    public struct RoundTimePacket : INetSerializable
    {
        public int playerId;
        public BombState state;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(playerId);
            writer.Put((int)state);
        }

        public void Deserialize(NetDataReader reader)
        {
            playerId = reader.GetInt();
            state = (BombState)reader.GetInt();
        }

        public override string ToString()
        {
            return $"{playerId} put bomb to state {state}";
        }
    }

    public class RoundTimePacketHandler : PacketHandler<RoundTimePacket>
    {
        public void Send(int playerId, BombState state)
        {
            var packet = new RoundTimePacket
            {
                playerId = playerId,
                state = state,
            };

            RequestSend(packet);
        }

        public override void OnReceive(RoundTimePacket packet)
        {
            Plugin.Logger.LogInfo($"Packet Type {nameof(RoundTimePacket)} is sending {packet.ToString()}");

        }
    }
}
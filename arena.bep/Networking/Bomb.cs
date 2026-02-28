using Comfort.Common;
using EFT;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.GameTypes;
using System.Linq;

namespace ifp.arena.bep.Networking
{
    public struct BombStatePacket : INetSerializable
    {
        public int playerId;
        public BombState state;
        public float timestamp;

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
            timestamp = reader.GetFloat();
        }

        public override string ToString()
        {
            return $"{playerId} state: {state} at {timestamp:F2}";
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
                timestamp = 0f
            };

            RequestSend(packet);
        }

        public override bool ServerValidation(ref BombStatePacket packet, NetPeer netPeer)
        {
            if (packet.state == BombState.Planted || packet.state == BombState.Defused || packet.state == BombState.Exploded)
            {
                return false;
            }

            float serverNow = (float)Singleton<AbstractGame>.Instance.GameTimer.SessionTime.Value.TotalSeconds;

            float latencySeconds = 0f;

            if (Singleton<FikaServer>.Instance != null)
            {
                var client = Singleton<NetPeer>.Instance.NetManager.FirstOrDefault(c => c.Id == netPeer.Id);

                if (client != null)
                {
                    latencySeconds = (client.Ping / 1000f) / 2f;
                }
            }

            packet.timestamp = serverNow - latencySeconds;

            Plugin.Logger.LogInfo($"Server: Player {packet.playerId} action {packet.state} validated at {packet.timestamp} (Lag Comp: {latencySeconds:F4}s)");

            return base.ServerValidation(ref packet, netPeer);
        }

        public override void OnReceive(BombStatePacket packet)
        {
            float now = (float)Singleton<AbstractGame>.Instance.GameTimer.SessionTime.Value.TotalSeconds;

            float timeElapsed = now - packet.timestamp;

            if (timeElapsed < 0) timeElapsed = 0;

            Plugin.Logger.LogInfo($"Received {packet.state}. Action started {timeElapsed:F2} seconds ago.");

            switch (packet.state)
            {
                case BombState.Planting:
                    // Example: Start the UI progress bar starting from 'timeElapsed'
                    // Coroutine_StartPlanting(initialProgress: timeElapsed, totalDuration: PlantDuration);
                    break;

                case BombState.Planted:
                    // The bomb is down. The Timer logic starts here.
                    // BombTimer = 45sec - timeElapsed;
                    break;

                case BombState.Exploded:
                    // Instant event, timestamp mainly useful for logs or killfeed sorting
                    break;
            }
        }
    }
}
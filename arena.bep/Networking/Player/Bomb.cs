using Comfort.Common;
using EFT;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.Core.FX;
using ifp.arena.bep.Core.Gamemode;
using ifp.arena.bep.GameTypes;
using ifp.arena.bep.networking.Base;
using ifp.arena.bep.networking.TimeSync;
using MemoryPack;
using UnityEngine;

namespace ifp.arena.bep.networking
{
    [MemoryPackable]
    public partial struct BombStatePacket : INetSerializable
    {
        public int playerId;
        public BombState state;
        // Vector3 is stored as three floats so MemoryPack can serialize it natively.
        public float posX, posY, posZ;
        public double timestamp;

        /// <summary>Convenience accessor — not serialized.</summary>
        [MemoryPackIgnore]
        public Vector3 position
        {
            get => new Vector3(posX, posY, posZ);
            set { posX = value.x; posY = value.y; posZ = value.z; }
        }

        public void Serialize(NetDataWriter writer) => MemoryPackHelper.Serialize(writer, this);
        public void Deserialize(NetDataReader reader) => this = MemoryPackHelper.Deserialize<BombStatePacket>(reader);
    }

    public class BombStatePacketHandler : PacketHandler<BombStatePacket>
    {
        public void Send(EFT.Player player, BombState state, Vector3 position)
        {
            var packet = new BombStatePacket
            {
                playerId = player.Id,
                state = state,
                timestamp = NetworkTime.ServerNowSeconds
            };
            packet.position = position;

            RequestSend(packet);
        }

        protected override bool ServerValidation(ref BombStatePacket packet, NetPeer peer)
        {
            return base.ServerValidation(ref packet, peer);
        }

        protected override void LocalPredictApproved(BombStatePacket packet)
        {
            PlayBombAudio(packet);
        }

        private void PlayBombAudio(BombStatePacket packet)
        {
            Player player = H.GetPlayer(packet.playerId);

            Vector3 pos = Vector3.zero;
            AudioClip clip = null;
            bool shouldPlay = true;

            switch (packet.state)
            {
                case BombState.None:
                    H.AudioHandler.CancelBombAudio();
                    break;
                case BombState.Planting:
                    pos = player.PlayerBody.transform.position;
                    clip = H.Sounds.Planting;
                    break;
                case BombState.Defusing:
                    pos = player.PlayerBody.transform.position;
                    clip = H.Sounds.Defusing;
                    break;
                case BombState.Defused:
                    pos = H.Arena.BombPlantedPosition;
                    clip = H.Sounds.Defused;
                    H.AudioHandler.StopBombTick();
                    break;
                case BombState.Planted:
                    pos = H.Arena.BombPlantedPosition;
                    clip = H.Sounds.Planted;
                    H.AudioHandler.StartBombTick(pos);
                    break;
                case BombState.Exploded:
                    pos = H.Arena.BombPlantedPosition;
                    clip = H.Sounds.Planted;
                    H.AudioHandler.StopBombTick();
                    break;
                default:
                    shouldPlay = false;
                    break;
            }

            if (shouldPlay && pos != Vector3.zero && clip != null)
            {
                H.AudioHandler.PlayBombAudio(pos, clip);
            }
        }

        protected override void WhenApproved(BombStatePacket packet, NetPeer peer)
        {
            H.Session.bombState = packet.state;
            D.Notify(packet.state);

            Player player = H.GetPlayer(packet.playerId);
            if (!player.IsYourPlayer)
            {
                PlayBombAudio(packet);
            }

            if (packet.state is BombState.Planted)
            {
                H.Arena.LastObjectivePlayerId = packet.playerId;
            }



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

using Cysharp.Threading.Tasks;
using EFT.InventoryLogic;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.Core;
using ifp.arena.bep.Core.Gamemode;
using ifp.arena.bep.networking.Base;
using MemoryPack;

namespace ifp.arena.bep.networking;

[MemoryPackable]
public partial struct BombAssignmentPacket : INetSerializable
{
    public int playerId;

    public void Serialize(NetDataWriter writer) => MemoryPackHelper.Serialize(writer, this);
    public void Deserialize(NetDataReader reader) => this = MemoryPackHelper.Deserialize<BombAssignmentPacket>(reader);
}

public class BombAssignmentPacketHandler : PacketHandler<BombAssignmentPacket>
{
    public BombAssignmentPacketHandler() : base(DeliveryMethod.ReliableOrdered, PacketAuthority.ServerOnly) { }

    public void Send()
    {
        if (H.Session.GetPlayersFromFaction(ifp.arena.shared.Faction.T).Count > 0)
        {
            var randomTerrorist = H.Session.GetPlayersFromFaction(ifp.arena.shared.Faction.T).RandomElement();

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
    protected override void WhenApproved(BombAssignmentPacket packet, NetPeer peer)
    {
        Item BombBackpack = IU.CreateItemFromTemplateId(SND_ModeRules.bombTemplateId);
        IU.ClientRequestGiveItem(BombBackpack).Forget();
    }
}
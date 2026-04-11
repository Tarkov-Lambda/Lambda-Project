using Cysharp.Threading.Tasks;
using EFT;
using EFT.InventoryLogic;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.Core;
using ifp.arena.bep.Core.Gamemode;
using PacketHandler;
using ifp.arena.shared;
using MemoryPack;

namespace ifp.arena.bep.networking;

[MemoryPackable]
public partial struct BombAssignmentPacket : INetSerializable, IAuthoredPacket
{
    [MemoryPackAllowSerialize]
    public Player Player { get; set; }

    public void Serialize(NetDataWriter writer) => MemoryPackWrapper.Serialize(writer, this);
    public void Deserialize(NetDataReader reader) => this = MemoryPackWrapper.Deserialize<BombAssignmentPacket>(reader);
}

public class BombAssignmentPacketHandler : PacketHandler<BombAssignmentPacket>
{
    public BombAssignmentPacketHandler() : base(DeliveryMethod.ReliableOrdered, PacketAuthority.ServerOnly) { }

    public void Send()
    {
        if (H.Session.GetPlayersFromFaction(Faction.T).Count > 0)
        {
            var randomTerrorist = H.Session.GetPlayersFromFaction(Faction.T).RandomElement();

            var packet = new BombAssignmentPacket { Player = randomTerrorist, };

            RequestSendToPlayer(packet, packet.Player.Id);
        }
    }

    public async UniTaskVoid SendDelayed(int delayMs = 50)
    {
        if (!H.IsServer) return;
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
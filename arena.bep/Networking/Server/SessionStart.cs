using Comfort.Common;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using ifp.arena.bep.Core.AssetBundleHandling;
using PacketHandler;
using MemoryPack;
using ifp.arena.bep.networking.TimeSync;
using EFT.InventoryLogic;
using Cysharp.Threading.Tasks;
using System;
using System.Linq;
using System.Collections.Generic;
using ifp.arena.bep.Core.Gamemode;

namespace ifp.arena.bep.networking;

[MemoryPackable]
public partial struct SessionStartPacket : INetSerializable
{
    public string level;
    public string gamemode;
    public Item[] presetItems;

    public void Serialize(NetDataWriter writer) => MemoryPackWrapper.Serialize(writer, this);
    public void Deserialize(NetDataReader reader) => this = MemoryPackWrapper.Deserialize<SessionStartPacket>(reader);
}

// Either when game mode has finished, or admin requests it. scoreboard is fresh.
public class SessionStartPacketHandler : PacketHandler<SessionStartPacket>
{
    public SessionStartPacketHandler() : base(DeliveryMethod.ReliableOrdered, PacketAuthority.Admin) { }

    protected override bool ShouldNotifyAboutRejection => true;

    private void PrepareForStart(SessionStartPacket packet)
    {
        foreach (var playerScore in H.Session.scoreboard.Values)
        {
            playerScore.SessionReset();
        }

        H.Session.factionWins.Clear();
        H.Session.matchState = MatchState.None;
        H.Session.level = packet.level;
        H.Session.InitializeScoreBoard();
    }

    public void Send()
    {
        if (!H.IsInRaid()) return;

        var packet = new SessionStartPacket
        {
            level = Plugin.level.Value,
            gamemode = Plugin.gamemode.Value,
        };

        // send manifest of all item assets that need to be loaded before starting
        if (H.IsServer)
        {
            packet.presetItems = PresetBundleHandler.Instance.itemsToLoad.ToArray();
        }

        DispatchPacket(packet);
    }

    // if a player was not present at the start of this session, send them the sitrep
    public void SendToPeer(NetPeer peer)
    {
        if (!H.IsInRaid()) return;

        var packet = new SessionStartPacket
        {
            level = H.Session.level,
        };

        if (H.IsServer)
        {
            packet.presetItems = PresetBundleHandler.Instance.itemsToLoad.ToArray();
        }

        DispatchPacketToPeer(packet, peer);
    }

    protected override bool EvaluatePacket(ref SessionStartPacket packet, NetPeer peer, out string rejectionReason)
    {
        Type type = GetLambdaGamemode(packet.gamemode);
        if (type != null)
        {
            H.Arena.gamemode = (LambdaGamemode)Activator.CreateInstance(type);
        }
        else
        {
            rejectionReason = "Can't find specified gamemode";
            return false;
        }

        packet.presetItems = PresetBundleHandler.Instance.itemsToLoad.ToArray();
        return base.EvaluatePacket(ref packet, peer, out rejectionReason);
    }

    protected override async void WhenApproved(SessionStartPacket packet, NetPeer peer)
    {
        PrepareForStart(packet);

        D.LogTransaction("Starting a match");

        if (packet.gamemode == "SNDGamemode")
        {
            H.Arena.gamemode = new SNDGamemode();
        }

        // Type type = GetLambdaGamemode(packet.gamemode);
        // if (type != null)
        // {
        //     H.Arena.activeRules = (LambdaGamemode)Activator.CreateInstance(type);
        // }
        // else
        // {
        //     Singleton<RaiseErrorPacketHandler>.Instance.Send("Can't find specified Gamemode");
        // }

        if (!H.IsClient)
        {
            Singleton<SessionManagerSyncPacketHandler>.Instance.Send();
            H.Arena.ChangeState(MatchState.Warmup);
        }

        NetworkTime.Reset();

        D.Dump(packet.presetItems);

        PresetBundleHandler.Instance.AddToCache(packet.presetItems);

        await UniTask.WhenAll(
            MapAssetBundleHandler.Instance.LoadMap(packet.level),
            PresetBundleHandler.Instance.LoadEverythingInCache()
        );

        if (!H.IsHeadless)
        {
            // Main player is ready
            Singleton<PlayerReadinessPacketHandler>.Instance.Send(PlayerReadinessState.Ready);
        }
    }

    private Type GetLambdaGamemode(string gamemode)
    {
        IEnumerable<Type> gamemodeTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .Where(t => typeof(LambdaGamemode).IsAssignableFrom(t)
                        && !t.IsAbstract);

        return gamemodeTypes.FirstOrDefault(t => t.Name == gamemode);
    }
}
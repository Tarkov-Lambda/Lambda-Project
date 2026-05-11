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
using EFT;
using ifp.arena.bep.Core;

namespace ifp.arena.bep.networking;

[MemoryPackable]
public partial struct SessionStartPacket : INetSerializable
{
    public string level;
    public string gamemode;
    public List<Item> asssetBundles; // this needs to change to ResourceKeys
    public bool isForLateJoiner;

    public void Serialize(NetDataWriter writer) => MemoryPackWrapper.Serialize(writer, this);
    public void Deserialize(NetDataReader reader) => this = MemoryPackWrapper.Deserialize<SessionStartPacket>(reader);
}

// Either when game mode has finished, or admin requests it. scoreboard is fresh.
public class SessionStartPacketHandler : PacketHandler<SessionStartPacket>
{
    protected override PacketAuthority Authority => PacketAuthority.Admin;

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
            packet.asssetBundles = PresetBundleHandler.Instance.itemsToLoad;
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
            gamemode = H.Gamemode.GetType().Name, // deal with it
        };

        if (H.IsServer)
        {
            packet.asssetBundles = PresetBundleHandler.Instance.itemsToLoad;
        }

        DispatchPacketToPeer(packet, peer);
    }

    protected override void MutateApprovedPacket(ref SessionStartPacket packet, NetPeer peer)
    {
        packet.asssetBundles = PresetBundleHandler.Instance.itemsToLoad;
    }

    protected override async void Apply(SessionStartPacket packet, NetPeer peer)
    {
        PrepareForStart(packet);

        H.Arena.gamemode = GetGamemodeByName(packet.gamemode);

        if (!H.IsClient)
        {
            Singleton<SessionManagerSyncPacketHandler>.Instance.Send();
            H.Arena.ChangeState(MatchState.Warmup);
        }

        NetworkTime.Reset();

        PresetBundleHandler.Instance.AddToCache(packet.asssetBundles);

        List<ResourceKey> firstPersonHands = new();
        foreach (var player in H.AllPlayers)
        {
            if (player.TryGetHandsResourceKey(out ResourceKey handsBundle))
            {
                firstPersonHands.Add(handsBundle);
            }
        }

        await UniTask.WhenAll(
            MapAssetBundleHandler.Instance.LoadMap(packet.level),
            PresetBundleHandler.Instance.LoadEverythingInCache(),
            firstPersonHands.LoadBundles()
        );

        if (!H.IsHeadless)
        {
            // we're ready chat
            Singleton<PlayerReadinessPacketHandler>.Instance.Send(PlayerReadinessState.Ready);
        }
    }

    // you gonna have to prefix patch this bro
    public LambdaGamemode GetGamemodeByName(string gamemodeName)
    {
        if (gamemodeName == "SNDGamemode")
        {
            return new SNDGamemode();
        }
        else if (gamemodeName == "DuelGamemode")
        {
            return new SNDGamemode();
        }
        else if (gamemodeName == "FFAGamemode")
        {
            return new FFAGamemode();
        }
        else if (gamemodeName == "HardpointGamemode")
        {
            return new HardpointGamemode();
        }
        else
        {
            return null;
        }
    }
}
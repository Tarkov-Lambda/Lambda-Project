using Comfort.Common;
using Lambda.Core.Main.AssetBundleHandling;
using PacketWarden;
using MemoryPack;
using EFT.InventoryLogic;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using Lambda.Core.Main.Gamemode;
using EFT;
using Lambda.Core.Main;
using PacketWarden.TimeSync;

namespace Lambda.Core.Networking;

[MemoryPackable]
public partial struct SessionStartPacket : IPacket
{
    public string level;
    public string gamemode;
    public List<Item> itemsToLoad; // this needs to change to ResourceKeys
    public bool isForLateJoiner;
}

// Either when game mode has finished, or admin requests it. scoreboard is fresh.
public class SessionStartPacketWarden : LambdaPacketWarden<SessionStartPacket>
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
            level = Plugin.Level.Value,
            gamemode = Plugin.Gamemode.Value,
        };

        DispatchPacket(packet);
    }

    // if a player was not present at the start of this session, send them the sitrep
    public void SendToPeer(int peerId)
    {
        if (!H.IsInRaid()) return;

        var packet = new SessionStartPacket
        {
            level = H.Session.level,
            gamemode = H.Gamemode.GetType().Name, // deal with it
        };

        if (H.IsServer)
        {
            packet.itemsToLoad = RuntimeBundleLoader.Instance.ItemsToLoad;
        }

        DispatchPacket(packet, peerId);
    }

    protected override void MutateApprovedPacket(ref SessionStartPacket packet, int peerId)
    {
        packet.itemsToLoad = RuntimeBundleLoader.Instance.ItemsToLoad;
    }

    protected override async void Apply(SessionStartPacket packet, int peerId)
    {
        PrepareForStart(packet);

        H.Arena.gamemode = GetGamemodeByName(packet.gamemode);

        if (!H.IsClient)
        {
            Singleton<SessionManagerSyncPacketWarden>.Instance.Send();
            H.Arena.ChangeState(MatchState.Warmup);
        }

        NetworkTime.Reset();

        RuntimeBundleLoader.Instance.AddToCache(packet.itemsToLoad);

        List<ResourceKey> firstPersonHands = new();
        foreach (var player in H.AllPlayers)
        {
            if (player.TryGetHandsResourceKey(out ResourceKey handsBundle))
            {
                firstPersonHands.Add(handsBundle);
            }
        }

        await UniTask.WhenAll(
            MapAssetBundleLoader.Instance.LoadMap(packet.level),
            RuntimeBundleLoader.Instance.LoadEverythingInCache(),
            firstPersonHands.LoadBundles()
        );

        if (!H.IsHeadless)
        {
            // we're ready chat
            Singleton<PlayerReadinessPacketWarden>.Instance.Send(PlayerReadinessState.Ready);
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
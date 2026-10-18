using Comfort.Common;
using Lambda.Core.Main.AssetBundleHandling;
using PacketWarden;
using MemoryPack;
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
    public List<string> templateIds;
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
            level = LambdaPlugin.Level.Value,
            gamemode = LambdaPlugin.Gamemode.Value,
        };

        DispatchPacket(ref packet);
    }

    // if a player was not present at the start of this session, send them the sitrep
    public void SendToPeer(int peerId)
    {
        if (!H.IsInRaid()) return;

        var packet = new SessionStartPacket
        {
            level = H.Session.level,
            gamemode = H.Gamemode.GetType().Name,
            isForLateJoiner = true,
        };

        DispatchPacket(ref packet, peerId);
    }

    protected override void MutateApprovedPacket(ref SessionStartPacket packet, int peerId)
    {
        packet.templateIds = RuntimeBundleLoader.Instance.TemplateIdsToLoad;
    }

    protected override async void Apply(SessionStartPacket packet, int peerId)
    {
        string previousLevel = H.Session.level;

        PrepareForStart(packet);

        H.Arena.gamemode?.Dispose();
        H.Arena.gamemode = GetGamemodeByName(packet.gamemode);
        H.Arena.inventoryManager = H.Arena.gamemode.InventoryManager;
        
        if (!H.IsClient)
        {
            Singleton<SessionManagerSyncPacketWarden>.Instance.Send();
            H.Arena.ServerPostMatchState(MatchState.Warmup);
        }

        NetworkTime.Reset();

        RuntimeBundleLoader.Instance.AddToCache(packet.templateIds);

        List<ResourceKey> firstPersonHands = new();
        foreach (var player in H.AllPlayers)
        {
            if (player.TryGetHandsResourceKey(out ResourceKey handsBundle))
            {
                firstPersonHands.Add(handsBundle);
            }
        }

        // a different map would otherwise stay loaded (and rendered) for the rest of the raid
        if (!string.IsNullOrEmpty(previousLevel) && previousLevel != packet.level)
        {
            if (!H.IsHeadless) Teleporter.Teleport(H.MainPlayer, "lobby", Faction.None);
            await MapAssetBundleLoader.Instance.UnloadMap(previousLevel);
        }

        await UniTask.WhenAll(
            MapAssetBundleLoader.Instance.LoadMap(packet.level),
            RuntimeBundleLoader.Instance.LoadEverythingInCache(),
            firstPersonHands.LoadBundles()
        );

        if (!H.IsHeadless)
        {
            Singleton<PlayerReadinessPacketWarden>.Instance.Send(PlayerReadinessState.Ready);

            if (packet.isForLateJoiner)
            {
                Singleton<FactionChangePacketWarden>.Instance.Send(Faction.Spectator);
            }
        }
    }

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
        else if (gamemodeName == "KnifeOnlyGamemode")
        {
            return new KnifeOnlyGamemode();
        }
        else if (gamemodeName == "AwpOnlyGamemode")
        {
            return new AwpOnlyGamemode();
        }
        else
        {
            return null;
        }
    }
}
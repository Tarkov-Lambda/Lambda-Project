using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using HarmonyLib;
using ifp.arena.bep.Core.Gamemode;
using ifp.arena.bep.GameTypes;
using ifp.arena.bep.Patches.Tarkov;
using System.Runtime.CompilerServices;
using EFT.UI;
using ifp.arena.bep.Core.FX;
using Fika.Core.Main.Players;
using ifp.arena.bep.Patches;
using Fika.Core.Main.Utils;


namespace ifp.arena.bep.Core;

// Helper class for singleton refences & helper functions
public static class Helpers
{
    public static GameWorld GameWorld => Singleton<GameWorld>.Instance;

    // public static Player MainPlayer => IsInRaid() ? GameWorld.MainPlayer : null;
    public static Player MainPlayer => GetMainPlayer();

    public static Inventory MainInventory => IsInRaid() ? MainPlayer.Inventory : null;
    public static InventoryController MainInventoryController => IsInRaid() ? MainPlayer.InventoryController : null;

    public static GUISounds EFTGUISounds => IsInRaid() ? Singleton<GUISounds>.Instance : null;

    public static AudioHandler AudioHandler => IsInRaid() ? Singleton<AudioHandler>.Instance : null;
    public static LambdaSounds Sounds => IsInRaid() ? Singleton<AudioHandler>.Instance.prefabSounds : null;

    public static BombHandler BombHandler => IsInRaid() ? Singleton<BombHandler>.Instance : null;

    public static FXHandler FXHandler => IsInRaid() ? Singleton<FXHandler>.Instance : null;

    public static SpectatorManager SpectatorManager => IsInRaid() ? Singleton<SpectatorManager>.Instance : null;

    public static bool IsHeadless => FikaBackendUtils.IsHeadless;
    public static bool IsClient => FikaBackendUtils.IsClient;
    public static bool IsServer => FikaBackendUtils.IsServer;

    public static PlayerScore MainPlayerScore => GetMainPlayerScore();
    public static List<Player> AllTeammates => H.Session.GetPlayersFromFaction(H.MainPlayerScore.Faction);
    public static List<PlayerScore> AllTeammateScores => H.Session.GetPlayerScoresFromFaction(H.MainPlayerScore.Faction);
    public static List<Player> AllPlayers => IsInRaid() ? GetAllPlayers() : new();

    public static IFikaNetworkManager FikaNet => Singleton<IFikaNetworkManager>.Instance;
    public static NetPacketProcessor NetPacketProcessor => GetPacketProcessor();
    public static NetManager NetManager => GetNetManager();

    public static ArenaController Arena => Singleton<ArenaController>.Instance;
    public static SessionManager Session => Arena.session;
    public static Dictionary<int, PlayerScore> Scoreboard => Singleton<ArenaController>.Instance.session.scoreboard;

    public static event Action<GameWorld> OnGameStarted
    {
        add => Patch_Gameworld_OnGameStarted.OnGameStarted += value;
        remove => Patch_Gameworld_OnGameStarted.OnGameStarted -= value;
    }

    public static event Action<GameWorld> OnGameDispose
    {
        add => Patch_Gameworld_OnDispose.OnDispose += value;
        remove => Patch_Gameworld_OnDispose.OnDispose -= value;
    }

    public static event Action OnNetworkManagerInitialized
    {
        add => Patch_FikaClient_OnNetworkSettingsPacketReceived.OnNetworkManagerInitialized += value;
        remove => Patch_FikaClient_OnNetworkSettingsPacketReceived.OnNetworkManagerInitialized -= value;
    }

    // bro thinks he's the main character
    private static Player GetMainPlayer()
    {
        try
        {
            if (H.IsHeadless)
            {
                D.Log("Headless trying to access MainPlayer. This is not supposed to happen.");
                D.Log(Environment.StackTrace);
                return null;
            }
            return IsInRaid() ? GameWorld.MainPlayer : null;
        }
        catch (Exception ex)
        {
            D.Dump(ex);
            D.Log(ex.StackTrace);
        }

        return null;
    }

    public static Player GetPlayer(int playerId)
    {
        if (!IsInRaid()) return null;
        return AllPlayers.FirstOrDefault(p => p.Id == playerId);
    }

    public static Player GetPlayer(string profileId)
    {
        if (!IsInRaid()) return null;
        return AllPlayers.FirstOrDefault(p => p.ProfileId == profileId);
    }

    public static PlayerScore GetPlayerScore(int playerId)
    {
        if (!IsInRaid()) return null;
        Scoreboard.TryGetValue(playerId, out var playerScore);
        return playerScore;
    }

    public static PlayerScore GetPlayerScore(Player player)
    {
        if (!IsInRaid()) return null;
        H.Scoreboard.TryGetValue(player.Id, out var playerScore);
        return playerScore;
    }

    public static PlayerScore GetMainPlayerScore()
    {
        if (!IsInRaid()) return null;
        Scoreboard.TryGetValue(MainPlayer.Id, out var playerScore);
        return playerScore;
    }

    private static List<Player> GetAllPlayers()
    {
        return GameWorld.AllAlivePlayersList;
    }

    public static bool IsInRaid()
    {
        // D.Log((GameWorld != null && GameWorld is not HideoutGameWorld).ToString());
        return GameWorld != null && GameWorld is not HideoutGameWorld;
    }


    public static NetPacketProcessor GetPacketProcessor()
    {
        var manager = FikaNet;
        if (manager == null) return null;

        var field = AccessTools.Field(FikaNet.GetType(), "_packetProcessor");

        return field?.GetValue(manager) as NetPacketProcessor;
    }

    public static NetManager GetNetManager()
    {
        var manager = FikaNet;
        if (manager == null) return null;

        var field = AccessTools.Field(FikaNet.GetType(), "_netServer");

        return field?.GetValue(manager) as NetManager;
    }

    public static Dictionary<string, int> GetCachedConnections()
    {
        var manager = FikaNet;
        if (manager == null) return null;

        var field = AccessTools.Field(FikaNet.GetType(), "_cachedConnections");

        return field?.GetValue(manager) as Dictionary<string, int>;
    }
}

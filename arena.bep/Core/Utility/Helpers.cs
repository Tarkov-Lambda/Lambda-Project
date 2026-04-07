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

namespace ifp.arena.bep.Core;

// Helper class for singleton refences & helper functions
public static class Helpers
{
    public static GameWorld GameWorld => Singleton<GameWorld>.Instance;
    public static Player MainPlayer => isInRaid() ? GameWorld.MainPlayer : null;
    public static Inventory MainInventory => isInRaid() ? MainPlayer.Inventory : null;
    public static InventoryController MainInventoryController => isInRaid() ? MainPlayer.InventoryController : null;

    public static GUISounds EFTGUISounds => isInRaid() ? Singleton<GUISounds>.Instance : null;

    public static AudioHandler AudioHandler => isInRaid() ? Singleton<AudioHandler>.Instance : null;
    public static LambdaSounds Sounds => isInRaid() ? Singleton<AudioHandler>.Instance.prefabSounds : null;

    public static BombHandler BombHandler => isInRaid() ? Singleton<BombHandler>.Instance : null;

    public static FXHandler FXHandler => isInRaid() ? Singleton<FXHandler>.Instance : null;

    public static SpectatorManager SpectatorManager => isInRaid() ? Singleton<SpectatorManager>.Instance : null;


    public static PlayerScore MainPlayerScore => GetMainPlayerScore();
    public static List<Player> AllTeammates => H.Session.GetPlayersFromFaction(H.MainPlayerScore.faction);
    public static List<PlayerScore> AllTeammateScores => H.Session.GetPlayerScoresFromFaction(H.MainPlayerScore.faction);
    public static List<Player> AllPlayers => isInRaid() ? GetAllPlayers() : new();

    public static IFikaNetworkManager FikaNet => Singleton<IFikaNetworkManager>.Instance;
    public static NetPacketProcessor NetPacketProcessor => GetPacketProcessor();
    public static NetManager NetManager => GetNetManager();

    public static ArenaController Arena => Singleton<ArenaController>.Instance;
    public static SessionInfo Session => Singleton<ArenaController>.Instance.session;
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
    public static Player GetMainPlayer()
    {
        if (!isInRaid()) return null;
        return GameWorld.MainPlayer;
    }

    public static Player GetPlayer(int playerId)
    {
        if (!isInRaid()) return null;
        return AllPlayers.FirstOrDefault(p => p.Id == playerId);
    }

    public static PlayerScore GetPlayerScore(int playerId)
    {
        if (!isInRaid()) return null;
        Arena.session.scoreboard.TryGetValue(playerId, out var playerScore);
        return playerScore;
    }

    public static PlayerScore GetMainPlayerScore()
    {
        if (!isInRaid()) return null;
        Arena.session.scoreboard.TryGetValue(MainPlayer.Id, out var playerScore);
        return playerScore;
    }

    private static List<Player> GetAllPlayers()
    {
        return GameWorld.AllAlivePlayersList;
    }

    public static bool isInRaid()
    {
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
}

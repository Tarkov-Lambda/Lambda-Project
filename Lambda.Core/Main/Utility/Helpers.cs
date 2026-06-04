using System;
using System.Collections.Generic;
using System.Linq;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using HarmonyLib;
using Lambda.Core.Main.Gamemode;
using Lambda.Core.GameTypes;
using Lambda.Core.Patches.Tarkov;
using EFT.UI;
using Lambda.Core.Main.FX;
using Fika.Core.Main.Utils;
using Systems.Effects;
using Lambda.Core.Main.Dying;
using Lambda.Core.Main.AssetBundleHandling;
using Fika.Core.Main.GameMode;


namespace Lambda.Core.Main;

// Helper class for singleton refences & helper functions
public static class Helpers
{
    // вертел я ваши анти паттерны
    #region EFT Singleton pointers
    public static GameWorld GameWorld                                     => Singleton<GameWorld>.Instance;
    public static Class308 TarkovClientISession                           => Singleton<ClientApplication<ISession>>.Instance.Session as Class308;
    public static TarkovApplication TarkovApp                             => Singleton<ClientApplication<ISession>>.Instance as TarkovApplication;

    public static PoolManagerClass PoolManagerClass                       => Singleton<PoolManagerClass>.Instance;
    public static Effects Effects                                         => Singleton<Effects>.Instance;
    public static RagdollCreator RagdollCreator                           => Singleton<RagdollCreator>.Instance;
    public static BetterAudio BetterAudio                                 => Singleton<BetterAudio>.Instance;
    public static CommonUI CommonUI                                       => Singleton<CommonUI>.Instance;
    public static PreloaderUI PreloaderUI                                 => Singleton<PreloaderUI>.Instance;
    public static BackendConfigSettingsClass BackendConfigSettingsClass   => Singleton<BackendConfigSettingsClass>.Instance;
    public static IFikaGame IFikaGame                                     => Singleton<IFikaGame>.Instance;
    public static SharedGameSettingsClass SharedGameSettingsClass         => Singleton<SharedGameSettingsClass>.Instance;
    public static CustomizationSolverClass CustomizationSolverClass       => Singleton<CustomizationSolverClass>.Instance;
    public static IEasyAssets IEasyAssets                                 => Singleton<IEasyAssets>.Instance;
    public static GUISounds EFTGUISounds                                  => Singleton<GUISounds>.Instance;
    #endregion

    #region EFT Main Player
    // EFT Main Player
    public static Player MainPlayer                                       => GetMainPlayer();
    public static Inventory MainInventory                                 => MainPlayer != null ? MainPlayer.Inventory : null;
    public static InventoryController MainInventoryController             => MainPlayer != null ? MainPlayer.InventoryController : null;
    #endregion

    #region Fika
    public static bool IsHeadless                                         => GameWorld is not HideoutGameWorld && FikaBackendUtils.IsHeadless;
    public static bool IsClient                                           => FikaBackendUtils.IsClient;
    public static bool IsServer                                           => GameWorld is HideoutGameWorld || FikaBackendUtils.IsServer;
    #endregion

    #region Internal
    public static ArenaController Arena                                   => Singleton<ArenaController>.Instance;
    public static SessionManager Session                                  => Arena.Session;
    public static LambdaGamemode Gamemode                                 => Arena.gamemode;
    public static bool IsArenaReady                                       => Arena?.Session != null;

    public static bool IsNightTime                                        => Gamemode is IGMWithNightMode nm && nm.IsNightTime;

    public static PlayerContextLookup Scoreboard                          => Singleton<ArenaController>.Instance.Session.scoreboard;
    public static PlayerContext MainPlayerScore                           => GetMainPlayerScore();
    public static List<Player> AllTeammates                               => Session.GetPlayersFromFaction(MainPlayerScore.Faction);
    public static List<PlayerContext> AllTeammateScores                   => Session.GetPlayerScoresFromFaction(MainPlayerScore.Faction);
    public static List<Player> AllPlayers                                 => GetAllPlayers();
    public static List<Player> AllPlayingPlayers                          => IsArenaReady ? GetAllPlayingPlayers() : new();

    public static AudioHandler AudioHandler                               => IsArenaReady ? Singleton<AudioHandler>.Instance : null;
    public static LambdaSounds Sounds                                     => IsArenaReady ? Singleton<AudioHandler>.Instance.PrefabSounds : null;
    public static MusicKit MusicKit                                       => IsArenaReady ? Singleton<AudioHandler>.Instance.MusicKitSounds : null;

    public static BombHandler BombHandler                                 => IsArenaReady ? Singleton<BombHandler>.Instance : null;
    public static FXHandler FXHandler                                     => IsArenaReady ? Singleton<FXHandler>.Instance : null;
    public static SpectatorManager SpectatorManager                       => IsArenaReady ? Singleton<SpectatorManager>.Instance : null;
    public static MapAssetBundleLoader MapAssetBundleHandler              => Singleton<MapAssetBundleLoader>.Instance;
    public static WeaponPresetManager WeaponPresetManager                 => Singleton<WeaponPresetManager>.Instance;
    #endregion

    #region 
    public static bool ShouldLog { get; } = true;
    #endregion


    #region Hooks
    // When the player fully spawns into the raid; after raid spawn countdown timer is 0 (Geneburn - Countdown reference)
    public static event Action OnGameStarted
    {
        add => Patch_Gameworld_OnGameStarted.OnGameStarted += value;
        remove => Patch_Gameworld_OnGameStarted.OnGameStarted -= value;
    }

    // When we are getting out of the raid
    public static event Action OnGameDispose
    {
        add => Patch_Gameworld_OnDispose.OnDispose += value;
        remove => Patch_Gameworld_OnDispose.OnDispose -= value;
    }

    // Happens when the user fully loads into the main menu and can interact with stash/traders/etc
    public static event Action AfterApplicationLoaded
    {
        add => TarkovApp.AfterApplicationLoaded += value;
        remove => TarkovApp.AfterApplicationLoaded -= value;
    }
    #endregion

    #region Player Lookup
    // bro thinks he's the main character
    private static Player GetMainPlayer()
    {
        try
        {
            if (IsHeadless)
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
        return AllPlayers?.FirstOrDefault(p => p.Id == playerId);
    }

    public static Player GetPlayer(string profileId)
    {
        return AllPlayers?.FirstOrDefault(p => p.Profile.ProfileId == profileId);
    }

    public static Player GetPlayerByName(string profileId)
    {
        return AllPlayers?.FirstOrDefault(p => p.ProfileId == profileId);
    }

    public static PlayerContext GetPlayerContext(Player player) => GetPlayerContext(player.Id);

    public static PlayerContext GetPlayerContext(int playerId)
    {
        if (!IsInRaid()) return null;
        if (!Scoreboard.TryGetValue(playerId, out var playerScore))
        {
            playerScore = new PlayerContext(playerId);
            Scoreboard.Add(playerId, playerScore);
        }
        return playerScore;
    }

    public static PlayerContext GetMainPlayerScore()
    {
        if (!IsInRaid()) return null;
        Scoreboard.TryGetValue(MainPlayer.Id, out var playerScore);
        return playerScore;
    }

    private static List<Player> GetAllPlayers()
    {
        return GameWorld != null ? GameWorld.AllAlivePlayersList : null;
    }

    private static List<Player> GetAllPlayingPlayers()
    {
        if (!IsInRaid()) return null;
        return GameWorld.AllAlivePlayersList.Where(player => player.Context.ReadyState != PlayerReadinessState.Disconnected && player.Context.Faction != Faction.Spectator).ToList();
    }
    #endregion

    public static bool IsMainMenuLoaded()
    {
        return H.TarkovClientISession?.Profile_1?.Id != null;
    }

    public static bool IsInRaid()
    {
        return GameWorld != null && GameWorld is not HideoutGameWorld;

// #if DEBUG
//         return GameWorld != null;
// #else
//         return GameWorld != null && GameWorld is not HideoutGameWorld;
// #endif
    }
}

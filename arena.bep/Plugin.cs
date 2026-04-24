using Audio.ReverbSubsystem;
using BepInEx;
using BepInEx.Configuration;
using Comfort.Common;
using Cysharp.Threading.Tasks;
using EFT;
using ifp.arena.bep.Core;
using ifp.arena.bep.Core.AssetBundleHandling;
using ifp.arena.bep.Core.Dying;
using ifp.arena.bep.Core.FX;
using ifp.arena.bep.Core.Gamemode;
using ifp.arena.bep.Core.UI;
using ifp.arena.bep.networking;
using ifp.arena.bep.networking.TimeSync;
using ifp.arena.bep.Patches;
using ifp.arena.bep.Patches.Tarkov;
using ifp.arena.bep.Patches.Tarkov.UI;
using ifp.arena.shared;
using MemoryPack;
using SPT.Reflection.Patching;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEngine;
using UnityEngine.LowLevel;

namespace ifp.arena.bep;

[BepInDependency("com.fika.core")]
[BepInPlugin("com.ifp.respawn", MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    public static new EFTLogger Logger;

    public static readonly string pathToBundles = Path.Combine(BepInEx.Paths.PluginPath, "ifp", "bundles");
    public static readonly string pathToDeps = Path.Combine(BepInEx.Paths.PluginPath, "ifp", "deps");
    public static readonly string pathToConfigs = Path.Combine(BepInEx.Paths.PluginPath, "ifp", "json");
    public static readonly string pathToLogs = Path.Combine(BepInEx.Paths.PluginPath, "ifp", "logs");

    internal static ConfigEntry<string> gamemode;
    internal static ConfigEntry<Faction> PrefferedFaction;
    internal static ConfigEntry<string> level;
    internal static ConfigEntry<string> Password;

    internal static ConfigEntry<bool> DisplayLogAsNotificationInGame;

    private ConfigEntry<KeyboardShortcut> DeathKey;
    private ConfigEntry<KeyboardShortcut> RestartKey;
    private ConfigEntry<KeyboardShortcut> DumpKey;

    private readonly List<ModulePatch> _patches = new();
    private readonly List<IDisposable> _disposables = new();
    private readonly List<Action> _releases = new();
    private readonly List<IMemoryPackFormatter> _memoryPackFormatters = new();

    private CancellationTokenSource _cts;

    private Action unregisterCommands;

    private UnityTicker _unityTickListner;

    private void RegisterPatch(ModulePatch patch)
    {
        patch.Enable();
        _patches.Add(patch);
    }

    private void RegisterMemoryPackFormatter<T>(IMemoryPackFormatter<T> formatter)
    {
        if (!MemoryPackFormatterProvider.IsRegistered<T>())
        {
            MemoryPackFormatterProvider.Register<T>(formatter as MemoryPackFormatter<T>);
        }
    }

    private void RegisterSingleton<T>() where T : class, IDisposable, new()
    {
        var instance = new T();
        Singleton<T>.Create(instance);
        _disposables.Add(instance);
        // Capture a typed release delegate now, while T is known at compile time.
        // PacketHandler<T>.Dispose() cannot clear Singleton<ConcreteType> because it only
        // has access to Singleton<PacketHandler<TPacket>> (a different generic slot).
        _releases.Add(() => Singleton<T>.Release(instance));
    }

    public async UniTask RegisterSingletonInRaid<T>() where T : class, IDisposable, new()
    {
        try
        {
            // await UniTask.WaitUntil(()                             => H.isInRaid(), cancellationToken: _cts.Token);
            await UniTask.WaitUntil(() => H.IsInRaid());
        }
        catch (OperationCanceledException)
        {
            return;
        }

        RegisterSingleton<T>();
    }

    async void Start()
    {
        PlayerLoopSystem playerLoop = PlayerLoop.GetCurrentPlayerLoop();
        PlayerLoopHelper.Initialize(ref playerLoop);

        Logger = new EFTLogger("Lambda", () => DisplayLogAsNotificationInGame.Value);
        BepInEx.Logging.Logger.Sources.Add(Logger);
        Logger.LogInfo("Load");
        InitConfiguration();

        // unregisterCommands = ConsoleScreen.Processor.RegisterCommandGroup<LambdaConsoleCommands>();

        _unityTickListner = new GameObject("UnityTickListener").AddComponent<UnityTicker>();
        DontDestroyOnLoad(_unityTickListner.gameObject);

        // AUDIO
        if (!H.IsHeadless)
        {
            SteamAudioInitializer.Initialize();
            RegisterPatch(new Patch_BetterAudio_SetProtagonist());                      // Attach SteamAudioListener to the local player's AudioListener transform whenever SetProtagonist is called (raid spawn).
            RegisterPatch(new Patch_AudioSource_set_spatialize());                      // Force internal spatialization off and redirect the real value to the DSP bridge
            RegisterPatch(new Patch_AudioSource_get_spatialize());                      // Force internal spatialization off and redirect the real value to the DSP bridge
            RegisterPatch(new Patch_AudioSource_set_spatialBlend());                    // Proxy spatialBlend calls to PhononDSPBridge
            RegisterPatch(new Patch_AudioSource_get_spatialBlend());                    // Proxy spatialBlend calls to PhononDSPBridge
            // RegisterPatch(new Patch_BetterSource_IncludeInOcclusionProcess());       // Bypass MetaXR Occlusion
            // RegisterPatch(new Patch_BetterAudio_FadeMixerVolume());
            // RegisterPatch(new Patch_BetterSource_ResetOcclusion());
            // RegisterPatch(new Patch_BetterSource_SetOcclusionVolumeFactor());

            RegisterPatch(new Patch_SimpleSource_Play());                               // Audio Source Routing
            RegisterPatch(new Patch_SuperSource_Play());                                // Audio Source Routing
            RegisterPatch(new Patch_ReverbSimpleSource_Play());                         // Audio Source Routing
            RegisterPatch(new Patch_ReverbSuperSource_Play());                          // Audio Source Routing
            RegisterPatch(new Patch_BetterSource_Play());                               // Audio Source Routing
            RegisterPatch(new Patch_BetterSource_PlayScheduled());                      // Audio Source Routing
            RegisterPatch(new Patch_SimpleSource_PlayScheduled());                      // Audio Source Routing
            RegisterPatch(new Patch_ReverbSuperSource_PlayScheduled());                 // Audio Source Routing

            RegisterPatch(new Patch_BetterSource_CheckBinauralAllowed());               // Audio Source Routing

            RegisterPatch(new Patch_BetterSource_SetOcclusionVolumeFactor());           // do not let anything be occluded
            RegisterPatch(new Patch_SpatialLowPassFilter_CalculateFrequency());         // bypass low filter muffling
            RegisterPatch(new Patch_SpatialHighPassFilter_CalculateFrequency());        // bypass high filter muffling
        }

        // RegisterPatch(new AudioDiscovery_Play_Patch());

        // TARKOV
        RegisterPatch(new Patch_Gameworld_OnGameStarted());                         // Hooks
        RegisterPatch(new Patch_Gameworld_OnDispose());                             // Hooks

        RegisterPatch(new Patch_ActiveHealthController_ApplyDamage());              // Caching last damage packet for subsequent death packet
        RegisterPatch(new Patch_ActiveHealthController_Kill());                     // Bypass Dying entirely

        // RegisterPatch(new Patch_PlayerBody_UpdatePlayerRenders());               // For hands models for spectator

        RegisterPatch(new Patch_Player_VisualPass());                               // Mapping ProceduralWeaponAnimation to the respective Player
        RegisterPatch(new Patch_ProceduralWeaponAnimation_ProcessEffectors());      // Reduce Bobbing/inertia motion for pistols
        RegisterPatch(new Patch_ProceduralWeaponAnimation_UpdateSwayFactors());     // Reduce Sway for pistols
        RegisterPatch(new Patch_ProceduralWeaponAnimation_CalculateCameraPosition()); // Reduce Bobbing/inertia motion for pistols
        RegisterPatch(new Patch_ProceduralWeaponAnimation_ZeroAdjustments());       // Procedural Blindfire Position
        RegisterPatch(new Patch_MovementContext_PlayerAnimatorSetBlindFire());      // Override Blindfire Animation
        RegisterPatch(new Patch_MovementContext_SetBlindFire());                    // Override Blindfire Animation, Set HandsController Blindfire and transmit a packet

        // RegisterPatch(new Patch_Class1396_method_3());                              // In edge cases where the hands controller gets bugged out - we hard reset it
        // RegisterPatch(new Patch_GClass2037_Start());                              // In edge cases where the hands controller gets bugged out - we hard reset it

        RegisterPatch(new Patch_MovementState_BlindFire());                         // Force Blindfire state regardless of movement state


        RegisterPatch(new Patch_Player_ShotReactions());                            // Headshot Audio
        RegisterPatch(new Patch_MovementContext_ManualUpdate());                    // Something something old movement
        // RegisterPatch(new NostalgiaPatrolFixExitPatch());
        // RegisterPatch(new NostalgiaPatrolFixEnterPatch());
        RegisterPatch(new Patch_MovementContext_GetNewState());                     // Change Movement State Classes
        // RegisterPatch(new Patch_MovementContext_SetAimingSlowdown());            // Do not slow down during aiming
        // RegisterPatch(new Patch_MovementContext_method_15());                    // Old Leaning

        RegisterPatch(new Patch_MovementContext_CanWalk());                         // For controller locking
        RegisterPatch(new Patch_MovementContext_CanJump());                         // For controller locking
        RegisterPatch(new Patch_ClientFirearmController_CanPressTrigger());         // For controller locking
        // RegisterPatch(new Patch_ApplyShot());



        RegisterPatch(new Patch_AmmoItemClass_RicochetChance());                    // Set ricochet chance to 0

        RegisterPatch(new Patch_InteractionContextHelper_GetAvailableActions_IInteractive()); // Looting Fake Corpses, Planting, Defusing


        RegisterPatch(new Patch_method_10());                                       // Fake Ragdoll error silencing
        // RegisterPatch(new Patch_FikaHealthBar_Awake());                          // Very sloppy way to do this and causes errors

        RegisterPatch(new Patch_Grenade_InvokeBlowUpEvent());                       // Bypassing explosion for custom grenades

        // Animation Patches
        // RegisterPatch(new Patch_GClass2963_Spawn());
        RegisterPatch(new Patch_BaseGrenadeHandsController_Drop());                 // Instant Grenade Unequip
        // RegisterPatch(new Patch_FirearmController_Spawn());
        RegisterPatch(new Patch_FirearmController_Drop());                          // Instant Weapon Unequip
        // RegisterPatch(new Patch_FirearmController_InitiateOperation());
        RegisterPatch(new Patch_EmptyHandsController_ExamineWeapon());              // Send Hands Examination Packet to other players

        // UI Patches
        UIPatches.Enable();
        RegisterPatch(new Patch_LoginUI_Awake());                                   // Are we logging in for the first time?

        // Fika Patches
        RegisterPatch(new Patch_FikaServer_OnCommonPlayerPacketReceived());         // Server-side preemptive death broadcasting
        RegisterPatch(new Patch_FikaServer_OnNetworkReceiveUnconnected());          // Allow clients to connect mid raid
        RegisterPatch(new Patch_FikaServer_OnConnectionRequest());                  // Allow clients to connect mid raid
        RegisterPatch(new Patch_FikaServer_StopNatIntroduceRoutine());              // Server keeps NAT Introduction during the raid
        RegisterPatch(new Patch_FikaServer_OnDestroy());                            // Stop NAT Introduction manually
        // RegisterPatch(new Patch_ClientInventoryOperationHandler_ReceiveStatusFromServer());

        // RegisterPatch(new Patch_FikaClient_OnNetworkSettingsPacketReceived());      // When IFikaNetworkManager is ready during mid session connect (really fucking stupid)

        RegisterPatch(new Patch_Button_set_enabled());                              // Allow clients to connect mid raid

        RegisterPatch(new Patch_ItemPositionSyncer_FixedUpdate());                  // Null safe guard
        RegisterPatch(new Patch_ItemPositionSyncer_NotifyDone());                   // Null safe guard

        // RegisterPatch(new Patch_ObservedPlayer_HandleDamagePacket());
        // RegisterPatch(new ObservedPlayer_PauseAllEffectsOnPlayer_Patch());
        // RegisterPatch(new ObservedPlayer_UnpauseAllEffectsOnPlayer_Patch());

        // Memory Pack Formatters
        RegisterMemoryPackFormatter(new PlayerFormatter());                         // Player -> Profile ID
        RegisterMemoryPackFormatter(new ItemFormatter());                           // Item -> Binary via EFT internals

        // Player Related Packets
        RegisterSingleton<PlayerKilledPacketHandler>();                             // Server/Client sends this if a Player dies (Server handles everyone's death to a bullet, client handles death to explosions, fall, etc)
        RegisterSingleton<FactionChangePacketHandler>();                            // Player swaps factions
        RegisterSingleton<BuyItemPacketHandler>();                                  // Player asks to spawn an item
        RegisterSingleton<HandsInspectPacketHandler>();                             // Hands Examination Packet
        RegisterSingleton<BlindFirePacketHandler>();                                // Procedural blindfire state synchronization
        RegisterSingleton<ReplenishPacketHandler>();                                // Player announcens a replenishment
        RegisterSingleton<BombAssignmentPacketHandler>();                           // Server tells a specific player to equip a bomb
        RegisterSingleton<CustomGrenadeExplosionPacketHandler>();                   // Explosion of a custom grenade
        RegisterSingleton<LadderNoisePacketHandler>();                              // Player plays a ladder noise
        RegisterSingleton<ForceRemoveItemPacketHandler>();                          // Announces removal of an item (if it's an armor plate, also recalculate the plate carrier)
        RegisterSingleton<AskForMoneyPacketHandler>();                              // Ask teammates for money to buy a specific item
        RegisterSingleton<GiftMoneyPacketHandler>();                                // Gift teammate money for a specific item (Beggar auto buys the item)
        // RegisterSingleton<InventoryCounterResyncPacketHandler>();                   // Manual current mongo id on observed players after manipulation

        // Session Related Packets
        RegisterSingleton<PlayerReadinessPacketHandler>();                          // Reporting whether the player is disconnected, connected, or ready to play on the map
        RegisterSingleton<LoadProgressPacketHandler>();                             // Reporting map loading progress
        RegisterSingleton<SessionManagerSyncPacketHandler>();                       // Server sends a snapshot of the entire session info (start of the match / on round end)
        RegisterSingleton<BombStatePacketHandler>();                                // Synchronization of bomb states (planting, planted, defusing, etc)
        RegisterSingleton<MatchStateSyncPacketHandler>();                           // Server changes match state (Warmup, Warmup End, Round Prepare, etc)
        RegisterSingleton<SessionStartPacketHandler>();                             // ENTRY POINT. This is where the server broadcast
        RegisterSingleton<AdminLoginPacketHandler>();                               // Allow clients to elevate their priviledges
        RegisterSingleton<TimeSynchronizationPacketHandler>();                      // UTC Time Synchronization
        RegisterSingleton<TimeSyncResponsePacketHandler>();                         // UTC Time Synchronization
        RegisterSingleton<PausePacketHandler>();                                    // Create a timeout
        RegisterSingleton<WeatherAndTimePacketHandler>();                           // Sync time of day between rounds

        // Internal Classses (order matters)
        RegisterSingleton<PresetBundleHandler>();                                   // Handler of preset item loading (stuff in the buy menu)
        RegisterSingleton<MapAssetBundleHandler>();                                 // Handler of map asset loading
        RegisterSingleton<RagdollCreator>();                                        // Fake Corpse Creation
        RegisterSingleton<PresetItemsCache>();                                      // Caching gun presets
        RegisterSingleton<WeaponPresetManager>();                                   // Initializes/Saves/Loads what gun preset is selected for in raid spawning
        RegisterSingleton<DefaultEquipmentManager>();                               // Collects

        try
        {
            RegisterSingleton<FXHandler>();                                         // Handler for Visual Effects (Mollies)
            RegisterSingleton<AudioHandler>();                                      // Handler for all custom Audio Effects (Ladder noise, headshots, music)
            RegisterSingleton<MusicHandler>();                                      // Listens to ArenaController and plays music when necessary
            // RegisterSingleton<RaymarchHandler>();
            RegisterSingleton<ArenaController>();                                   // MAIN ENTRY POINT
            RegisterSingleton<SpectatorManager>();                                  // Spectator functionality

            _disposables.Add(new UIManager());  // not a singleton (fuck you) - тебе ебало разбить сука? не зли меня

            var warmup = typeof(Ladder);
            await RegisterSingletonInRaid<LadderManager>();                    // Overwrites Player Controller on Ladder Collision and moves them.
            await RegisterSingletonInRaid<BombHandler>();                      // Handler for the entirety of Bomb's lifecycle
            await RegisterSingletonInRaid<HardpointZoneManager>();             // Manages Hardpoint zones and synchronization

            // H.MainPlayer.NukeResetHands();
        }
        catch (Exception ex)
        {
            D.Dump(ex);
            D.Log(ex.StackTrace);
        }

#if DEBUG
        // _disposables.Add(new DynamicClassTracer(typeof(BetterAudio)));
#endif

    }

    private void InitConfiguration()
    {
        PrefferedFaction = Config.Bind("", "Preffered Faction", Faction.None, "Faction swaps only happen after the round end");

        level = Config.Bind("Admin", "Map Name", "", "");
        gamemode = Config.Bind("Admin", "Gamemode", "SNDGamemode", "");
        Password = Config.Bind("Admin", "Password", "", "");

        DisplayLogAsNotificationInGame = Config.Bind("Debug", "DisplayLogAsNotificationInGame", false);

        DeathKey = Config.Bind("Debug", "Death Key", new KeyboardShortcut(KeyCode.F2));
        RestartKey = Config.Bind("Debug", "RestartKey", new KeyboardShortcut(KeyCode.F1));
        DumpKey = Config.Bind("Debug", "Unfuck Key", new KeyboardShortcut(KeyCode.F4), "Use this key to unfuck yourself");
    }

    private void Update()
    {
        if (DeathKey.Value.IsDown())
        {
            EDamageType type = EDamageType.Fall;
            H.MainPlayer.ActiveHealthController.Kill(type);
        }
        if (RestartKey.Value.IsDown())
        {
            Singleton<SessionStartPacketHandler>.Instance.Send();
        }

        if (DumpKey.Value.IsDown())
        {
            H.MainPlayer.NukeResetHands();
            H.MainPlayer.ForceUnlockInventory();
        }
    }

    void OnDestroy()
    {
        Logger.LogInfo("Unload");

        // unregisterCommands.Invoke();

        // H.MainPlayer.MovementContext.ExitOverridenState();
        // RunStateClass idleState                                    = new RunStateClass(H.MainPlayer.MovementContext);
        // H.MainPlayer.MovementContext.ProcessStateEnter(idleState);

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        // if (H.GameWorld != null && H.GameWorld is not HideoutGameWorld)
        // {
        //     H.Session.matchState                                   = MatchState.None;
        //     Teleporter.Teleport(H.MainPlayer, "lobby");
        // }

        foreach (var patch in _patches)
        {
            if (patch is IDisposable disposablePatch)
                disposablePatch.Dispose();

            patch.Disable();
        }

        UIPatches.Disable();

        _patches.Clear();

        foreach (var disposable in _disposables)
            disposable.Dispose();

        _disposables.Clear();

        if (_unityTickListner != null)
            GameObject.Destroy(_unityTickListner.gameObject);

        // Release concrete singleton slots AFTER all Dispose() calls so that
        // singletons can still safely access each other's Instance during teardown.
        foreach (var release in _releases)
            release();

        _releases.Clear();

        BepInEx.Logging.Logger.Sources.Add(Logger);
        Logger = null;
    }
}
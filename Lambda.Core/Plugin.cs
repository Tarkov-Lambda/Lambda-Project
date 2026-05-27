using BepInEx;
using BepInEx.Configuration;
using Comfort.Common;
using Cysharp.Threading.Tasks;
using EFT;
using HarmonyLib;
using Lambda.Core.Main;
using Lambda.Core.Main.AssetBundleHandling;
using Lambda.Core.Main.Dying;
using Lambda.Core.Main.FX;
using Lambda.Core.Main.Gamemode;
using Lambda.Core.Main.UI;
using Lambda.Core.Networking;
using Lambda.Core.Networking.Commands;
using Lambda.Core.Patches;
using Lambda.Core.Patches.Tarkov;
using Lambda.Core.Patches.Tarkov.UI;
using MemoryPack;
using SPT.Reflection.Patching;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEngine;
using UnityEngine.LowLevel;

namespace Lambda.Core;

[BepInDependency("com.fika.core")]
[BepInDependency("com.ifp.PacketWarden")]
[BepInPlugin("com.ifp.lambda", MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class LambdaPlugin : BaseUnityPlugin
{
    public static new EFTLogger Logger;

    public static readonly string pathToBundles = Path.Combine(BepInEx.Paths.PluginPath, "ifp", "bundles");
    public static readonly string pathToMaps = Path.Combine(BepInEx.Paths.PluginPath, "ifp", "maps");
    public static readonly string pathToBinaries = Path.Combine(BepInEx.Paths.PluginPath, "ifp", "Binaries");
    public static readonly string pathToConfigs = Path.Combine(BepInEx.Paths.PluginPath, "ifp", "Configuration");
    public static readonly string pathToLogs = Path.Combine(BepInEx.Paths.PluginPath, "ifp", "logs");

    internal static ConfigEntry<string> Gamemode;
    internal static ConfigEntry<string> Level;
    internal static ConfigEntry<string> Password;
    internal static ConfigEntry<float> MusicVolume;
    internal static ConfigEntry<string> ClanTag;

    internal static ConfigEntry<bool> DisplayLogAsNotificationInGame;

    private ConfigEntry<KeyboardShortcut> StartKey;
    private ConfigEntry<KeyboardShortcut> DeathKey;
    private ConfigEntry<KeyboardShortcut> MapReloadKey;
    private ConfigEntry<KeyboardShortcut> UnfuckKey;
    private ConfigEntry<KeyboardShortcut> NoclipKey;

    private readonly List<ModulePatch> _patches = new();
    private readonly List<IDisposable> _disposables = new();
    private readonly List<Action> _releases = new();

    private CancellationTokenSource _cts;

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
            MemoryPackFormatterProvider.Register(formatter as MemoryPackFormatter<T>);
        }
    }

    private void RegisterSingleton<T>() where T : class, IDisposable, new()
    {
        D.Log($"Registering {typeof(T).Name}");
        var instance = new T();
        Singleton<T>.Create(instance);
        _disposables.Add(instance);
        _releases.Add(() => Singleton<T>.Release(instance));
    }

    public async UniTask RegisterSingletonInRaid<T>() where T : class, IDisposable, new()
    {
        try
        {
            await UniTask.WaitUntil(() => H.IsInRaid(), cancellationToken: _cts.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        RegisterSingleton<T>();
    }

    // Никому не верь кроме монолиту и братьям твоим, никому.
    async void Start()
    {
        PlayerLoopSystem playerLoop = PlayerLoop.GetCurrentPlayerLoop();
        PlayerLoopHelper.Initialize(ref playerLoop);

        Logger = new EFTLogger("Lambda", () => DisplayLogAsNotificationInGame.Value);
        BepInEx.Logging.Logger.Sources.Add(Logger);
        Logger.LogInfo("Load");
        InitConfiguration();

        _cts = new CancellationTokenSource();

        // unregisterCommands = ConsoleScreen.Processor.RegisterCommandGroup<LambdaConsoleCommands>();

        _unityTickListner = new GameObject("UnityTickListener").AddComponent<UnityTicker>();
        DontDestroyOnLoad(_unityTickListner.gameObject);

        // TARKOV
        RegisterPatch(new Patch_Gameworld_OnGameStarted());                         // Hooks
        RegisterPatch(new Patch_Gameworld_OnDispose());                             // Hooks
        // RegisterPatch(new Patch_Gameworld_RegisterLoot());                       // Hooks
        // RegisterPatch(new Patch_LootItem_Init());                                // Creating Corpse doesn't create dogtag

        // Damage and death behavior modification
        RegisterPatch(new Patch_ActiveHealthController_ApplyDamage());              // Cache last damage packet, multiply flame damage, negate blacked out limbs damage
        RegisterPatch(new Patch_ActiveHealthController_Kill());                     // Bypass Dying entirely

        RegisterPatch(new Patch_ActiveHealthController_DoFracture());              // Do not add fracture
        RegisterPatch(new Patch_ActiveHealthController_DoBleedGeneric());          // Do not add bleeding
        RegisterPatch(new Patch_ActiveHealthController_DoBleed_HeavyBleeding());   // Do not add bleeding
        RegisterPatch(new Patch_ActiveHealthController_DoBleed_LightBleeding());   // Do not add bleeding

        // Procedural BlindFire
        RegisterPatch(new Patch_ProceduralWeaponAnimation_ProcessEffectors());      // Reduce Bobbing/inertia motion for pistols
        RegisterPatch(new Patch_ProceduralWeaponAnimation_UpdateSwayFactors());     // Reduce Sway for pistols
        RegisterPatch(new Patch_ProceduralWeaponAnimation_CalculateCameraPosition()); // Reduce Bobbing/inertia motion for pistols
        RegisterPatch(new Patch_ProceduralWeaponAnimation_ZeroAdjustments());       // Procedural Blindfire Position
        RegisterPatch(new Patch_MovementState_BlindFire());                         // Force Blindfire state regardless of movement state
        RegisterPatch(new Patch_MovementContext_PlayerAnimatorSetBlindFire());      // Override Blindfire Animation
        RegisterPatch(new Patch_MovementContext_SetBlindFire());                    // Override Blindfire Animation, Set HandsController Blindfire and transmit a packet
        RegisterPatch(new Patch_MovementContext_ApplyDamageByVaulting());           // No vault damage on blacked out limbs
        RegisterPatch(new Patch_GamePlayerOwner_TranslateCommand());                // Prevent Resetting Freelook from cancelling BetterPlantStateClass

        // RegisterPatch(new Patch_Class1396_method_3());                           // In edge cases where the hands controller gets bugged out - we hard reset it
        // RegisterPatch(new Patch_GClass2037_Start());                             // In edge cases where the hands controller gets bugged out - we hard reset it

        RegisterPatch(new Patch_Player_ShotReactions());                            // Headshot Audio
        RegisterPatch(new Patch_Player_UpdateTick());                               // If the item can't be picked up -> unlock the player movement
        RegisterPatch(new Patch_MovementContext_ManualUpdate());                    // Something something old movement
        // RegisterPatch(new NostalgiaPatrolFixExitPatch());
        // RegisterPatch(new NostalgiaPatrolFixEnterPatch());
        RegisterPatch(new Patch_MovementContext_GetNewState());                     // Change Movement State Classes
        RegisterPatch(new Patch_MovementContext_SetAimingSlowdown());               // move in ads slightly faster
        RegisterPatch(new Patch_MovementContext_method_15());                       // Faster Leaning

        // RegisterPatch(new Patch_RunStateClass_Jump());                           // Just jump or whatever (il error for some reason)

        RegisterPatch(new Patch_MovementContext_CanWalk());                         // For controller locking (Also allow running during meds)
        RegisterPatch(new Patch_MovementContext_CanJump());                         // For controller locking (Also allow running during meds)
        RegisterPatch(new Patch_MovementContext_CanProne());                        // No proning allowed
        RegisterPatch(new Patch_FirearmController_SetTriggerPressed());             // For controller locking

        RegisterPatch(new Patch_SmokeGrenade_Init());                               // Smoke tuning
        RegisterPatch(new Patch_Effects_GetEmissionEffect());                       // Smoke tuning

        RegisterPatch(new Patch_MagazineItemClass_GetAmmoCountByLevel());           // Mags autosearched
        RegisterPatch(new Patch_BackpackItemClass_Constructor());                   // Bomb doesn't have space
        RegisterPatch(new Patch_VisorsItemClass_Constructor());                     // Blindness protection out the wazoo
        RegisterPatch(new Patch_ThrowWeapItemClass_FragmentsCount());               // Molly no fragments
        RegisterPatch(new Patch_ThrowWeapItemClass_MinFragmentDamage());            // Molly no fragments
        RegisterPatch(new Patch_ThrowWeapItemClass_MaxFragmentDamage());            // Molly no fragments
        RegisterPatch(new Patch_ThrowWeapItemClass_MinTimeToContactExplode());      // Molly no fragments

        RegisterPatch(new Patch_AmmoItemClass_RicochetChance());                    // Set ricochet chance to 0
        RegisterPatch(new Patch_InteractionContextHelper_GetAvailableActions());    // Looting Fake Corpses, Planting, Defusing
        RegisterPatch(new Patch_method_10());                                       // Fake Ragdoll error silencing
        RegisterPatch(new Patch_Grenade_Init());                                    // Force explode mollies after delay
        RegisterPatch(new Patch_Grenade_InvokeBlowUpEvent());                       // Server Generates Molly BFS Pattern on Explosion

        // Animation Patches
        // RegisterPatch(new Patch_GClass2963_Spawn());
        RegisterPatch(new Patch_BaseGrenadeHandsController_Drop());                 // Instant Grenade Unequip
        // RegisterPatch(new Patch_FirearmController_Spawn());
        RegisterPatch(new Patch_FirearmController_Drop());                          // Instant Weapon Unequip
        RegisterPatch(new Patch_EmptyHandsController_ExamineWeapon());              // Send Hands Examination Packet to other players

        // UI Patches
        UIPatches.Enable();
        RegisterPatch(new Patch_SearchableView_Awake());                            // Remove Secured Container Slot in raid
        RegisterPatch(new Patch_Class1841_method_0());                              // FOV slider overwrite
        RegisterPatch(new Patch_GameSettingsTab_Show());                            // FOV slider overwrite
        // RegisterPatch(new Patch_GameGraphicsTab_MaxFramerateLobbyLimit());          // Crank max lobby framerate to 120 fps
        // RegisterPatch(new Patch_GameGraphicsTab_MaxFramerateGameLimit());           // Crank max game framerate to 345 fps (at 350 fps jumps break)
        GameGraphicsClass.MaxFramerateGameLimit = 345;
        GameGraphicsClass.MaxFramerateLobbyLimit = 120;
        RegisterPatch(new Patch_Button_set_enabled());                              // FIKA ONLY: Allow clients to connect mid raid

        // Camera Patches
        RegisterPatch(new Patch_Class640_method_1());                               // Remove visual painkiller effect

        // Fika Patches
        RegisterPatch(new Patch_FikaServer_OnCommonPlayerPacketReceived());         // Server-side preemptive death broadcasting
        RegisterPatch(new Patch_FikaServer_OnNetworkReceiveUnconnected());          // Allow clients to connect mid raid
        RegisterPatch(new Patch_FikaServer_OnConnectionRequest());                  // Allow clients to connect mid raid
        RegisterPatch(new Patch_FikaServer_StopNatIntroduceRoutine());              // Server keeps NAT Introduction during the raid
        RegisterPatch(new Patch_FikaServer_OnDestroy());                            // Stop NAT Introduction manually

        RegisterPatch(new Patch_HostGameController_GetHostLootItems());             // no bytes for loot items (some nre fix idk)
        RegisterPatch(new Patch_FikaServer_OnNetworkSettingsPacketReceived());      // snapshotter timestamp reconnect fix

        RegisterPatch(new Patch_ItemPositionSyncer_FixedUpdate());                  // Null safe guard
        RegisterPatch(new Patch_ItemPositionSyncer_NotifyDone());                   // Null safe guard
        RegisterPatch(new Patch_ClientInventoryOperationHandler_ReceiveStatusFromServer()); // failed operation triggers inventory controller resynchronization

        RegisterPatch(new Patch_FikaConfig_ToNumber());
        // RegisterPatch(new Patch_FikaGlobals_ToNumber());                            // Crank movement send rate to 60hz
        // RegisterPatch(new Patch_AdaptiveJitterBuffer_CurrentDelay());               // Reduce Adaptive Jitter Buffer's base delay to 20ms
        // RegisterPatch(new Patch_PlayerSnapshotter_Constructor());                   // Increase Player Snapshotter's packet capacity to 64 (TRANSPILER)
        // RegisterPatch(new Patch_PlayerSnapshotter_AddSnapshot());                   // Increase Player Snapshotter's packet capacity to 64 (TRANSPILER)
        // RegisterPatch(new Patch_PlayerSnapshotter_GetInterpolationIndices());       // Increase Player Snapshotter's packet capacity to 64 (TRANSPILER)

        // RegisterPatch(new ObservedPlayer_POV_Getter_Patch());                        //
        // RegisterPatch(new ObservedPlayer_VisualPass_Patch());                       // Player camera leans with the observed player during spectation


        // Memory Pack Formatters
        RegisterMemoryPackFormatter(new ItemPlacementFormatter());

        // Player Related Packets
        RegisterSingleton<PlayerKilledPacketWarden>();                              // Server/Client sends this if a Player dies (Server handles everyone's death to a bullet, client handles death to explosions, fall, etc)
        RegisterSingleton<FactionChangePacketWarden>();                             // Player swaps factions
        RegisterSingleton<BuyItemPacketWarden>();                                   // Player asks to spawn an item
        RegisterSingleton<HandsInspectPacketWarden>();                              // Hands Examination Packet
        RegisterSingleton<BlindFirePacketWarden>();                                 // Procedural blindfire state synchronization
        RegisterSingleton<ReplenishPacketWarden>();                                 // Player announcens a replenishment
        RegisterSingleton<SmokeExplosionPacketWarden>();                            // Smoke Bloom Broadcast
        RegisterSingleton<MolotovExplosionPacketWarden>();                          // Molotov BFS Explosion Broadcast
        RegisterSingleton<LadderNoisePacketWarden>();                               // Player plays a ladder noise
        RegisterSingleton<ForceRemoveItemPacketWarden>();                           // Announces removal of an item (if it's an armor plate, also recalculate the plate carrier)
        RegisterSingleton<AskForMoneyPacketWarden>();                               // Ask teammates for money to buy a specific item
        RegisterSingleton<GiftMoneyPacketWarden>();                                 // Gift teammate money for a specific item (Beggar auto buys the item)
        RegisterSingleton<EquipmentResyncPacketWarden>();                           // Resynchronize Inventory Controller
        RegisterSingleton<DictateTeleportPacketWarden>();                           // Tell the player to teleport somewhere
        RegisterSingleton<ChatMessagePacketWarden>();                               // Player sends a message
        RegisterSingleton<AskForBombPriorityPacketWarden>();                        // Player requesting to be the bomb carry for the foreseeable rounds
        RegisterSingleton<ClanTagResyncPacketWarden>();                             // Player sets new clan tag

        // Session Related Packets
        RegisterSingleton<PlayerReadinessPacketWarden>();                           // Reporting whether the player is disconnected, connected, or ready to play on the map
        RegisterSingleton<LoadProgressPacketWarden>();                              // Reporting map loading progress
        RegisterSingleton<SessionManagerSyncPacketWarden>();                        // Server sends a snapshot of the entire session info (start of the match / on round end)
        RegisterSingleton<BombStatePacketWarden>();                                 // Synchronization of bomb states (planting, planted, defusing, etc)
        RegisterSingleton<MatchStateSyncPacketWarden>();                            // Server changes match state (Warmup, Warmup End, Round Prepare, etc)
        RegisterSingleton<SessionStartPacketWarden>();                              // ENTRY POINT. This is where admins start the game
        RegisterSingleton<SessionStopPacketWarden>();                               // Stop match prematurely and teleport everyone back to the lobby
        RegisterSingleton<AdminLoginPacketWarden>();                                // Allow clients to elevate their priviledges
        RegisterSingleton<SessionPausePacketWarden>();                              // Create a timeout
        RegisterSingleton<WeatherAndTimeSyncPacketWarden>();                        // Sync time of day between rounds
        RegisterSingleton<ServerMessagePacketWarden>();                              // Server sends an announcement message
        RegisterSingleton<ShutdownAnnouncementPacketWarden>();                      // Server announces imminent shutdown
        RegisterSingleton<MoneyResyncPacketWarden>();                               // Server dictates new money amount for a specific player
        RegisterSingleton<ReconnectSnapshotterResetPacketWarden>();                 // Duct Tape

        RegisterSingleton<AssetBundleLoadPacketWarden>();                           // Server broadcasts a batch of asset bundles to load
        RegisterSingleton<AssetBundleLoadFinishedPacketWarden>();                   // Player responds back saying they loaded a specific batch of asset bundles

        try
        {
            // Internal Classses (order matters)
            RegisterSingleton<RuntimeBundleLoader>();                               // Handler of preset item loading (stuff in the buy menu)
            RegisterSingleton<MapAssetBundleLoader>();                              // Handler of map asset loading
            RegisterSingleton<RagdollCreator>();                                    // Fake Corpse Creation
            RegisterSingleton<PresetItemsCache>();                                  // Caching gun presets
            RegisterSingleton<WeaponPresetManager>();                               // Initializes/Saves/Loads what gun preset is selected for in raid spawning
            RegisterSingleton<ClientEquipmentManager>();                            // 

            RegisterSingleton<FXHandler>();                                         // Handler for Visual Effects (Mollies)
            RegisterSingleton<AudioHandler>();                                      // Handler for all custom Audio Effects (Ladder noise, headshots, music)
            RegisterSingleton<MusicHandler>();                                      // Listens to ArenaController and plays music when necessary
            RegisterSingleton<SpectatorManager>();                                  // Spectator functionality
            RegisterSingleton<ArenaController>();                                   // MAIN ENTRY POINT!!!!!!!

            _disposables.Add(new UIManager());

            // errors happen if these get loaded before raid has started
            RegisterSingletonInRaid<LadderManager>().Forget();                    // Overwrites Player Controller on Ladder Collision and moves them.
            RegisterSingletonInRaid<BombHandler>().Forget();                      // Handler for the entirety of Bomb's lifecycle
            RegisterSingletonInRaid<HardpointZoneManager>().Forget();             // Manages Hardpoint zones and synchronization
        }
        catch (Exception ex)
        {
            D.Dump(ex);
            D.Log(ex.StackTrace);
        }

        // if (H.IsInRaid())
        // {
        //     H.MainPlayer.MovementContext.PlantState = H.MainPlayer.MovementContext.GetNewState(EPlayerState.Plant, false);
        //     H.MainPlayer.MovementContext.PlantState.Name = EPlayerState.Plant;
        //     H.MainPlayer.MovementContext.PlantState.AnimatorStateHash = -1;

        //     // LambdaAudioRoomController.Instance.TriggerChange();
        // }

        // TODO: make this initialization more in line with the rest of the project
        UniTask.RunOnThreadPool(async () =>
        {
            await UniTask.WaitUntil(() => H.IsMainMenuLoaded());
            ChatCommandInterceptor.Initialize();
        }, cancellationToken: _cts.Token).Forget();
    }

    private void InitConfiguration()
    {
        Level = Config.Bind("Admin", "Map Name", "samplevel", "");
        Gamemode = Config.Bind("Admin", "Gamemode", "SNDGamemode", "");
        Password = Config.Bind("Admin", "Password", "", "");

        MusicVolume = Config.Bind("", "Music Volume", 0.25f, "");
        ClanTag = Config.Bind("", "Clan Tag", "", "");

        DisplayLogAsNotificationInGame = Config.Bind("Debug", "DisplayLogAsNotificationInGame", false);

        StartKey = Config.Bind("Debug", "Start key", new KeyboardShortcut(KeyCode.F1));
        DeathKey = Config.Bind("Debug", "Suicide Key", new KeyboardShortcut(KeyCode.F2));
        MapReloadKey = Config.Bind("Debug", "Map Hot-Reload Key", new KeyboardShortcut(KeyCode.F3));
        UnfuckKey = Config.Bind("Debug", "Hands Unfucking Key", new KeyboardShortcut(KeyCode.F4));
        NoclipKey = Config.Bind("Noclip", "", new KeyboardShortcut(KeyCode.CapsLock));
    }


    private void Update()
    {
        if (StartKey.Value.IsDown())
        {
            Singleton<SessionStartPacketWarden>.Instance.Send();
        }

        if (DeathKey.Value.IsDown())
        {
            EDamageType type = EDamageType.Fall;
            H.MainPlayer.ActiveHealthController.Kill(type);
        }

        if (MapReloadKey.Value.IsDown())
        {
            MapAssetBundleLoader.Instance.ReloadMap(H.Session.level).Forget();
        }

        if (UnfuckKey.Value.IsDown())
        {
            PU.OpenEyes();
            Singleton<EquipmentResyncPacketWarden>.Instance.Send(H.MainPlayer);
        }

        // TODO: This needs to get the fuck out of here
        if (NoclipKey.Value.IsDown() && H.MainPlayerScore?.IsAdmin != false)
        {
            Noclip.ToggleNoclip();

            if (!Noclip.IsEnabled)
            {
                H.MainPlayer.MovementContext.ResetFlying();
            }
        }

        if (Noclip.IsEnabled && H.IsInRaid() && H.MainPlayerScore?.IsAdmin != false)
        {
            Noclip.ProcessNoclipFrame();
        }
    }

    void OnDestroy()
    {
        Logger.LogInfo("Unload");

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

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

        BepInEx.Logging.Logger.Sources.Remove(Logger);
        Logger = null;
    }
}

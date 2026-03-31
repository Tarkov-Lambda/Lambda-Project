using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
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
using SPT.Reflection.Patching;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;



namespace ifp.arena.bep;

[BepInDependency("com.fika.core")]
[BepInPlugin("com.ifp.respawn", MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    public static new ManualLogSource Logger;

    internal static ConfigEntry<GameModes> GameMode;
    internal static ConfigEntry<Faction> PrefferedFaction;
    internal static ConfigEntry<string> MapName;
    internal static ConfigEntry<string> Password;

    private ConfigEntry<KeyboardShortcut> DeathKey;
    private ConfigEntry<KeyboardShortcut> RestartKey;

    private GameObject TracerOverlay;

    private readonly List<ModulePatch> _patches = new();
    private readonly List<IDisposable> _disposables = new();

    private CancellationTokenSource _cts;

    private void RegisterPatch(ModulePatch patch)
    {
        patch.Enable();
        _patches.Add(patch);
    }

    public void RegisterSingleton<T>() where T : class, IDisposable, new()
    {
        var instance = new T();
        Singleton<T>.Create(instance);
        _disposables.Add(instance);
    }

    public async UniTask RegisterSingletonInRaid<T>() where T : class, IDisposable, new()
    {
        try
        {
            // await UniTask.WaitUntil(() => H.isInRaid(), cancellationToken: _cts.Token);
            await UniTask.WaitUntil(() => H.isInRaid());

        }
        catch (OperationCanceledException)
        {
            return;
        }

        RegisterSingleton<T>();
    }

    async Task Start()
    {
        Logger = base.Logger;
        Logger.LogInfo("Load");
        InitConfiguration();

        // RegisterPatch(new Patch_AudioSettings_GetSpatializerPluginName());
        SteamAudioInitializer.Initialize();
        RegisterPatch(new Patch_MetaSpatialAudioSource_ManualUpdate()); // Swap Meta XR spatializer for our SteamAudioSpatialAudioSource on every BetterSource that is initialized from a prefab pool.
        RegisterPatch(new Patch_BetterSource_Init()); // Swap Meta XR spatializer for our SteamAudioSpatialAudioSource on every BetterSource that is initialized from a prefab pool.
        RegisterPatch(new Patch_BetterAudio_SetProtagonist()); // Attach SteamAudioListener to the local player's AudioListener transform whenever SetProtagonist is called (raid spawn / respawn).

        // TARKOV
        RegisterPatch(new Patch_Gameworld_OnGameStarted()); // Hooks
        RegisterPatch(new Patch_Gameworld_OnDispose()); // Hooks

        RegisterPatch(new Patch_ActiveHealthController_Kill()); // Bypass Dying entirely

        // RegisterPatch(new Patch_PlayerBody_UpdatePlayerRenders()); // For hands models for spectator

        RegisterPatch(new Patch_Player_VisualPass()); // Mapping ProceduralWeaponAnimation instances to players
        RegisterPatch(new Patch_ProceduralWeaponAnimation_ProcessEffectors()); // Reduce Bobbing/inertia motion for pistols
        RegisterPatch(new Patch_ProceduralWeaponAnimation_UpdateSwayFactors()); // Reduce Sway for pistols
        RegisterPatch(new Patch_ProceduralWeaponAnimation_CalculateCameraPosition()); // Reduce Bobbing/inertia motion for pistols
        RegisterPatch(new Patch_ProceduralWeaponAnimation_ZeroAdjustments()); // Procedural Blindfire Position
        RegisterPatch(new Patch_MovementContext_PlayerAnimatorSetBlindFire()); // Override Blindfire Animation
        RegisterPatch(new Patch_MovementContext_SetBlindFire()); // Override Blindfire Animation, Set HandsController Blindfire and transmit a packet
        RegisterPatch(new Patch_MovementState_BlindFire()); // Force Blindfire state regardless of movement state


        RegisterPatch(new Patch_Player_ShotReactions()); // Smooth Speed Tweak

        RegisterPatch(new Patch_MovementContext_ManualUpdate()); // Smooth Speed Tweak
        // RegisterPatch(new NostalgiaPatrolFixExitPatch());
        // RegisterPatch(new NostalgiaPatrolFixEnterPatch());
        RegisterPatch(new Patch_MovementContext_GetNewState()); // Change Movement State Classes
        // RegisterPatch(new Patch_MovementContext_SetAimingSlowdown()); // Do not slow down during aiming
        // RegisterPatch(new Patch_MovementContext_method_15()); // Old Leaning

        RegisterPatch(new Patch_CanWalk()); // For controller locking
        RegisterPatch(new Patch_CanJump()); // For controller locking
        RegisterPatch(new Patch_CanPressTrigger()); // For controller locking
        // RegisterPatch(new Patch_ApplyShot());

        RegisterPatch(new Patch_ActiveHealthController_ApplyDamage()); // Caching last damage packet for death
        RegisterPatch(new Patch_AmmoItemClass_RicochetChance()); // Set ricochet chance to 0

        // RegisterPatch(new Patch_BackendConfigSettingsClass_AimPunchMagnitude()); // Set aimpunch to 0

        // RegisterPatch(new Patch_SearchableItemItemClass_IsSearched()); // Planting (PlaceItem)

        // RegisterPatch(new Patch_InteractionContextHelper_GetAvailableActions_PlaceItemTrigger()); // Planting (PlaceItem)
        RegisterPatch(new Patch_InteractionContextHelper_GetAvailableActions_IInteractive()); // Defusing (Tripwire)


        RegisterPatch(new Patch_method_10()); // Fake Ragdoll error silencing
        // RegisterPatch(new Patch_FikaHealthBar_Awake()); // Very sloppy way to do this and causes errors

        RegisterPatch(new Patch_EmptyHandsController_ExamineWeapon()); // Other players see you inspecting hands


        RegisterPatch(new Patch_Grenade_InvokeBlowUpEvent()); // Bypassing explosion for custom grenades

        //--------------- ANIMATIONS --------------- //
        // RegisterPatch(new Patch_GClass2963_Spawn());
        RegisterPatch(new Patch_BaseGrenadeHandsController_Drop()); // Instant Grenade Unequip
        // RegisterPatch(new Patch_FirearmController_Spawn());
        RegisterPatch(new Patch_FirearmController_Drop()); // Instant Weapon Unequip
        // RegisterPatch(new Patch_FirearmController_InitiateOperation());
        //------------------------------------------ //

        RegisterPatch(new Patch_CommonUI_Awake()); // Action Hook
        RegisterPatch(new Patch_ItemsTabController_Show()); // Action Hook
        RegisterPatch(new Patch_EftGamePlayerOwner_TranslateInventoryScreenInput()); // Inventory opening control (for when we reset inv or hold tab for scoreboard)


        //--------------- FIKA --------------- //
        // RegisterPatch(new Patch_FikaServer_OnCommonPlayerPacketReceived()); // Server-side preemptive death broadcasting
        RegisterPatch(new Patch_ItemPositionSyncer_FixedUpdate());
        RegisterPatch(new Patch_ItemPositionSyncer_NotifyDone());

        // RegisterPatch(new ObservedPlayer_CreateObservedPlayer_Transpiler());
        // RegisterPatch(new Patch_ObservedPlayer_HandleDamagePacket());
        // RegisterPatch(new ObservedPlayer_PauseAllEffectsOnPlayer_Patch());
        // RegisterPatch(new ObservedPlayer_UnpauseAllEffectsOnPlayer_Patch());
        //------------------------------------------ //

        //--------------- NETWORK --------------- //
        // Player
        RegisterSingleton<PlayerKilledPacketHandler>();
        RegisterSingleton<FactionChangePacketHandler>();
        RegisterSingleton<SpawnItemPacketHandler>();
        RegisterSingleton<HandsInspectPacketHandler>();
        RegisterSingleton<BlindFirePacketHandler>();
        RegisterSingleton<ReplenishPacketHandler>();
        RegisterSingleton<BombAssignmentPacketHandler>();
        RegisterSingleton<CustomGrenadeExplosionPacketHandler>();
        RegisterSingleton<LadderNoisePacketHandler>();

        // Session
        RegisterSingleton<SessionInfoPacketHandler>();
        RegisterSingleton<BombStatePacketHandler>();
        RegisterSingleton<MatchStateSyncPacketHandler>();
        RegisterSingleton<RestartPacketHandler>();
        RegisterSingleton<AssetLoadStatePacketHandler>();
        RegisterSingleton<AdminLoginPacketHandler>();
        RegisterSingleton<TimeSyncRequestPacketHandler>();
        RegisterSingleton<TimeSyncResponsePacketHandler>();
        RegisterSingleton<PausePacketHandler>();
        //------------------------------------------ //

        // Internal Classses (order matters)

        RegisterSingleton<MapAssetBundleHandler>();
        RegisterSingleton<RagdollCreator>();
        RegisterSingleton<ImmutableItemsCache>();
        RegisterSingleton<PresetManager>();

        try
        {
            RegisterSingleton<FXHandler>();
            RegisterSingleton<AudioHandler>();
            RegisterSingleton<MusicHandler>();
            // RegisterSingleton<RaymarchHandler>();
            RegisterSingleton<UIManager>();
            RegisterSingleton<ArenaController>();
            RegisterSingleton<SpectatorManager>();

            var warmup = typeof(Ladder);
            await RegisterSingletonInRaid<LadderEventManager>(); // this lifecycle needs refactor asap
            await RegisterSingletonInRaid<BombHandler>();
        }
        catch (Exception ex)
        {
            D.Dump(ex);
            D.Log(ex.StackTrace);
        }

#if DEBUG
        // _disposables.Add(new DynamicClassTracer(typeof(AudioSettings)));
#endif

    }

    private void InitConfiguration()
    {
        PrefferedFaction = Config.Bind("", "Preffered Faction", Faction.None, "Faction swaps only happen after the round end");

        MapName = Config.Bind("Admin", "Map Name", "", "");
        GameMode = Config.Bind("Admin", "Gamemode", GameModes.SND, "");
        Password = Config.Bind("Admin", "Password", "", "");

        DeathKey = Config.Bind("Debug", "Death Key", new KeyboardShortcut(KeyCode.F2));
        RestartKey = Config.Bind("Debug", "RestartKey", new KeyboardShortcut(KeyCode.F1));
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
            Singleton<RestartPacketHandler>.Instance.Send();
        }
    }

    void OnDestroy()
    {
        Logger.LogInfo("Unload");
        UnityEngine.Object.Destroy(TracerOverlay);

        // H.MainPlayer.MovementContext.ExitOverridenState();
        // RunStateClass idleState = new RunStateClass(H.MainPlayer.MovementContext);
        // H.MainPlayer.MovementContext.ProcessStateEnter(idleState);

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        // if (H.GameWorld != null && H.GameWorld is not HideoutGameWorld)
        // {
        //     H.Session.matchState = MatchState.None;
        //     Teleporter.Teleport(H.MainPlayer, "lobby");
        // }

        foreach (var patch in _patches)
            patch.Disable();

        _patches.Clear();

        foreach (var disposable in _disposables)
            disposable.Dispose();

        _disposables.Clear();

        Logger = null;
    }
}
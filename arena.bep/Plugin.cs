using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Comfort.Common;
using Cysharp.Threading.Tasks;
using EFT;
using HarmonyLib;
using ifp.arena.bep.Core;
using ifp.arena.bep.Core.AssetBundleHandling;
using ifp.arena.bep.Core.Dying;
using ifp.arena.bep.Core.FX;
using ifp.arena.bep.Core.Gamemode;
using ifp.arena.bep.Core.MovementStates;
using ifp.arena.bep.Core.UI;
using ifp.arena.bep.GameTypes;
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
using UnityEngine;

#if DEBUG
// To log H.Dump object name
namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
    sealed class CallerArgumentExpressionAttribute : Attribute
    {
        public string ParameterName { get; }

        public CallerArgumentExpressionAttribute(string parameterName)
        {
            ParameterName = parameterName;
        }
    }
}
#endif


namespace ifp.arena.bep
{
    [BepInDependency("com.fika.core")]
    [BepInPlugin("com.ifp.respawn", MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public static new ManualLogSource Logger;

        internal static ConfigEntry<bool> Active;
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

        void Start()
        {
            Logger = base.Logger;
            Logger.LogInfo("Load");
            InitConfiguration();

            // TARKOV
            RegisterPatch(new Patch_Gameworld_OnGameStarted()); // Hooks
            RegisterPatch(new Patch_Gameworld_OnDispose()); // Hooks

            RegisterPatch(new Patch_Kill()); // Bypass Dying entirely

            RegisterPatch(new Patch_ProceduralWeaponAnimation_ZeroAdjustments()); // Procedural Blindfire Position
            RegisterPatch(new Patch_MovementContext_PlayerAnimatorSetBlindFire()); // Override Blindfire Animation
            RegisterPatch(new Patch_MovementContext_SetBlindFire()); // Override Blindfire Animation, Set HandsController Blindfire and transmit a packet
            RegisterPatch(new Patch_MovementState_BlindFire()); // Force Blindfire state regardless of movement state

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

            RegisterPatch(new Patch_ApplyDamage()); // Caching last damage packet for death
            RegisterPatch(new Patch_AmmoItemClass_RicochetChance()); // Set ricochet chance to 0

            RegisterPatch(new Patch_InteractionContextHelper_GetAvailableActions()); // Planting/Defusing

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
            RegisterPatch(new Patch_FikaServer_OnCommonPlayerPacketReceived()); // Server-side preemptive death broadcasting
            RegisterPatch(new Patch_ItemPositionSyncer_FixedUpdate()); 
            RegisterPatch(new Patch_ItemPositionSyncer_NotifyDone()); 
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
            RegisterSingleton<AssetBundleHandler>();
            RegisterSingleton<RagdollCreator>();
            RegisterSingleton<ArenaController>();
            RegisterSingleton<ImmutableItemsCache>();
            RegisterSingleton<UIManager>();
            RegisterSingleton<FXHandler>();


            var warmup = typeof(Ladder);
            RegisterSingletonInRaid<LadderEventManager>().Forget();

#if DEBUG
            // _disposables.Add(new DynamicClassTracer(typeof(MovementContext)));
            TracerOverlay = new GameObject("Arena Gamesession");
            TracerOverlay.AddComponent<TracerOverlay>();
            DontDestroyOnLoad(TracerOverlay);
#endif

        }

        private void InitConfiguration()
        {
            Active = Config.Bind("", "Active", true, "Whether or not the plugin is active");
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

            if (H.GameWorld != null && H.GameWorld is not HideoutGameWorld)
            {
                H.Session.roundState = MatchState.None;
                Teleporter.Teleport(H.MainPlayer, "lobby");
            }

            foreach (var patch in _patches)
                patch.Disable();

            _patches.Clear();

            foreach (var disposable in _disposables)
                disposable.Dispose();

            _disposables.Clear();

            Logger = null;
        }
    }
}
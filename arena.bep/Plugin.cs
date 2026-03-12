using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using HarmonyLib;
using ifp.arena.bep.Core;
using ifp.arena.bep.Core.AssetBundleHandling;
using ifp.arena.bep.Core.Dying;
using ifp.arena.bep.Core.Gamemode;
using ifp.arena.bep.Core.UI;
using ifp.arena.bep.GameTypes;
using ifp.arena.bep.networking;
using ifp.arena.bep.networking.TimeSync;
using ifp.arena.bep.Patches.Tarkov;
using ifp.arena.bep.Patches.Tarkov.UI;
using ifp.arena.shared;
using SPT.Reflection.Patching;
using System;
using System.Collections.Generic;
using UnityEngine;

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
        internal static ConfigEntry<string> MusicKitPath;

        private ConfigEntry<KeyboardShortcut> DeathKey;
        private ConfigEntry<KeyboardShortcut> RestartKey;

        private GameObject TracerOverlay;

        private readonly List<ModulePatch> _patches = new();
        private readonly List<IDisposable> _disposables = new();

        private void RegisterPatch(ModulePatch patch)
        {
            patch.Enable();
            _patches.Add(patch);
        }

        private void RegisterPacket<T>() where T : class, IDisposable, new()
        {
            var instance = new T();
            Singleton<T>.Create(instance);
            _disposables.Add(instance);
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

            RegisterPatch(new Patch_CanWalk()); // For controller locking
            RegisterPatch(new Patch_CanJump()); // For controller locking
            RegisterPatch(new Patch_CanPressTrigger()); // For controller locking
            // RegisterPatch(new Patch_ApplyShot());

            RegisterPatch(new Patch_ApplyDamage());
            RegisterPatch(new Patch_AmmoItemClass_RicochetChance()); // Set ricochet chance to 0

            RegisterPatch(new Patch_InteractionContextHelper_GetAvailableActions()); // Planting/Defusing


            RegisterPatch(new Patch_method_10()); // Fake Ragdoll error silencing
            // RegisterPatch(new Patch_FikaHealthBar_Awake()); // Very sloppy way to do this and causes errors

            RegisterPatch(new Patch_EmptyHandsController_ExamineWeapon()); // Other players see you inspecting hands
            // RegisterPatch(new Patch_FirearmController_InitiateShot());


            //--------------- ANIMATIONS --------------- //
            // RegisterPatch(new Patch_GClass2963_Spawn());
            RegisterPatch(new Patch_BaseGrenadeHandsController_Drop()); // Instant Grenade Unequip
            // RegisterPatch(new Patch_FirearmController_Spawn());
            RegisterPatch(new Patch_FirearmController_Drop()); // Instant Weapon Unequip
            // RegisterPatch(new Patch_FirearmController_InitiateOperation());
            //------------------------------------------ //


            RegisterPatch(new Patch_CommonUI_Awake());
            RegisterPatch(new Patch_ItemsTabController_Show());
            RegisterPatch(new Patch_EftGamePlayerOwner_TranslateInventoryScreenInput());

            RegisterPacket<AssetBundleHandler>();
            RegisterPacket<RagdollCreator>();

            // FIKA
            // RegisterPatch(new Patch_FikaClient_OnCommonPlayerPacketReceived());

            // NETWORK
            RegisterPacket<PlayerKilledPacketHandler>();
            RegisterPacket<FactionChangePacketHandler>();
            RegisterPacket<SpawnItemPacketHandler>();

            RegisterPacket<SessionInfoPacketHandler>();
            RegisterPacket<BombStatePacketHandler>();
            RegisterPacket<BombAssignmentPacketHandler>();
            RegisterPacket<MatchStateSyncPacketHandler>();
            RegisterPacket<RestartPacketHandler>();
            RegisterPacket<AssetLoadStatePacketHandler>();
            RegisterPacket<AdminLoginPacketHandler>();
            RegisterPacket<HandsInspectPacketHandler>();
            RegisterPacket<ReplenishPacketHandler>();
            RegisterPacket<TimeSyncRequestPacketHandler>();
            RegisterPacket<TimeSyncResponsePacketHandler>();
            RegisterPacket<PausePacketHandler>();

            RegisterPacket<ArenaController>();

            RegisterPacket<ImmutableItemsCache>();
            RegisterPacket<UIManager>();

            // _disposables.Add(new DynamicClassTracer(typeof(ArenaController)));

            TracerOverlay = new GameObject("Arena Gamesession");
            TracerOverlay.AddComponent<TracerOverlay>();
            DontDestroyOnLoad(TracerOverlay);

        }

        private void InitConfiguration()
        {
            Active = Config.Bind("", "Active", true, "Whether or not the plugin is active");
            PrefferedFaction = Config.Bind("", "Preffered Faction", Faction.None, "Faction swaps only happen after the round end");
            MusicKitPath = Config.Bind("", "MusicKitPath", "", "C:/Users/mrimf/Documents/GitHub/fika-arena/audio/music");

            MapName = Config.Bind("Admin", "Map Name", "", "");
            GameMode = Config.Bind("Admin", "Gamemodes", GameModes.FFA, "");
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

            if (H.GameWorld != null && H.GameWorld is not HideoutGameWorld)
            {
                ArenaController.Instance.session.roundState = MatchState.None;
                Teleporter.Teleport(H.GameWorld.MainPlayer);
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
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Comfort.Common;
using Dissonance;
using EFT;
using Fika.Core.Networking.LiteNetLib.Utils;
using HarmonyLib;
using ifp.arena.bep.Core.AssetBundleHandling;
using ifp.arena.bep.Core.Dying;
using ifp.arena.bep.Core.Gamemode;
using ifp.arena.bep.GameTypes;
using ifp.arena.bep.networking;
using ifp.arena.bep.networking.TimeSync;
using ifp.arena.bep.Patches;
using ifp.arena.bep.Patches.Fika;
using ifp.arena.bep.Patches.Tarkov;
using ifp.arena.shared;
using SPT.Reflection;
using SPT.Reflection.Patching;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using static System.Collections.Specialized.BitVector32;

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

        private ConfigEntry<KeyboardShortcut> SessionInfoKey;
        private ConfigEntry<KeyboardShortcut> RoundStateChangeKey;
        private ConfigEntry<KeyboardShortcut> RestartKey;

        public static async Task Delay(int ms)
        {
            await Task.Delay(ms);
        }

        private readonly List<ModulePatch> _patches = new();
        private readonly List<IDisposable> _disposables = new();

        private void RegisterPatch(ModulePatch patch)
        {
            patch.Enable();
            _patches.Add(patch);
        }

        private void CreateSingleton<T>() where T : class, IDisposable, new()
        {
            var instance = new T();
            Singleton<T>.Create(instance);
            _disposables.Add(instance);
        }

        void Start()
        {
            Logger = base.Logger;
            Plugin.Logger.LogInfo("Load");

            RegisterPatch(new Patch_Gameworld_OnGameStarted());
            RegisterPatch(new Patch_Gameworld_OnDispose());

            RegisterPatch(new Patch_Kill());

            RegisterPatch(new Patch_CanWalk());
            RegisterPatch(new Patch_CanJump());
            RegisterPatch(new Patch_CanPressTrigger());
            RegisterPatch(new Patch_ApplyShot());
            RegisterPatch(new Patch_ApplyDamage());


            RegisterPatch(new Patch_OnCommonPlayerPacketReceived());


            CreateSingleton<PlayerKilledPacketHandler>();
            CreateSingleton<FactionChangePacketHandler>();

            CreateSingleton<SessionInfoPacketHandler>();
            CreateSingleton<BombStatePacketHandler>();
            CreateSingleton<RoundStateSyncPacketHandler>();
            CreateSingleton<RestartPacketHandler>();
            CreateSingleton<AssetLoadStatePacketHandler>();

            CreateSingleton<TimeSyncRequestPacketHandler>();
            CreateSingleton<TimeSyncResponsePacketHandler>();

            CreateSingleton<Base>();
            CreateSingleton<AssetBundleHandler>();
            CreateSingleton<RagdollCreator>();

            InitConfiguration();
        }

        private void InitConfiguration()
        {
            PrefferedFaction = Config.Bind("", "Preffered Faction", Faction.None, "Faction swaps only happen after the round end");

            GameMode = Config.Bind("Admin", "Gamemodes", GameModes.FFA, "");
            MapName = Config.Bind("Admin", "Map Name", "", "");

            Active = Config.Bind("", "Active", true, "Whether or not the plugin is active");
            SessionInfoKey = Config.Bind("Debug", "SessionInfoKey", new KeyboardShortcut(KeyCode.F3));
            RoundStateChangeKey = Config.Bind("Debug", "RoundStateChangeKey", new KeyboardShortcut(KeyCode.F2));
            RestartKey = Config.Bind("Debug", "RestartKey", new KeyboardShortcut(KeyCode.F1));

        }

        private void Update()
        {
            if (SessionInfoKey.Value.IsDown())
            {
                Singleton<SessionInfoPacketHandler>.Instance.Send();
            }
            if (RoundStateChangeKey.Value.IsDown())
            {
                Singleton<Base>.Instance.session.roundState = (RoundState)(((int)Singleton<Base>.Instance.session.roundState + 1) % 6); ;
                Singleton<RoundStateSyncPacketHandler>.Instance.Send(Singleton<Base>.Instance.session.roundState, 5d);
                Logger.LogInfo(Singleton<Base>.Instance.session.roundState);
            }
            if (RestartKey.Value.IsDown())
            {
                Singleton<RestartPacketHandler>.Instance.Send();
            }
        }

        void OnDestroy()
        {
            Plugin.Logger.LogInfo("Unload");

            Base.Instance.session.roundState = RoundState.None;
            Teleporter.Teleport(Singleton<GameWorld>.Instance.MainPlayer);

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
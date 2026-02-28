using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Comfort.Common;
using Dissonance;
using Fika.Core.Networking.LiteNetLib.Utils;
using HarmonyLib;
using ifp.arena.bep.GameTypes;
using ifp.arena.bep.Networking;
using ifp.arena.bep.Patches;
using ifp.arena.shared;
using System.Collections.Generic;
using SPT.Reflection;
using SPT.Reflection.Patching;
using System;
using System.Threading.Tasks;
using UnityEngine;
using ifp.arena.bep.Core.Dying;
using ifp.arena.bep.Patches.Tarkov;

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

        pActiveHealthController_Kill patchKill;
        Patch_CanWalk canWalk;
        ModulePatch jopa;

        Stack<ModulePatch> patches;

        RagdollCreator ragdollCreator;

        public static async Task Delay(int ms)
        {
            await Task.Delay(ms);
        }

        void Start()
        {
            patches = new Stack<ModulePatch>();

            Logger = base.Logger;
            Plugin.Logger.LogInfo("Load");

            patches.AddItem(new Patch_Gameworld_OnGameStarted());
            patches.Peek().Enable();

            patches.AddItem(new pActiveHealthController_Kill());
            patches.Peek().Enable();

            patches.AddItem(new Patch_CanWalk());
            patches.Peek().Enable();

            patches.AddItem(new Patch_CanJump());
            patches.Peek().Enable();

            patches.AddItem(new Patch_CanPressTrigger());
            patches.Peek().Enable();

            // Packet Handlers
            Singleton<PlayerKilledPacketHandler>.Create(new PlayerKilledPacketHandler());
            Singleton<SessionInfoPacketHandler>.Create(new SessionInfoPacketHandler());
            Singleton<BombStatePacketHandler>.Create(new BombStatePacketHandler());

            Singleton<RestartPacketHandler>.Create(new RestartPacketHandler());
            Singleton<RoundStatePacketHandler>.Create(new RoundStatePacketHandler());

            Singleton<BaseGameMode>.Create(new BaseGameMode());

            ragdollCreator = new RagdollCreator();

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
                Singleton<BaseGameMode>.Instance.session.roundState = (RoundState)(((int)Singleton<BaseGameMode>.Instance.session.roundState + 1) % 6); ;
                Singleton<RoundStatePacketHandler>.Instance.Send(Singleton<BaseGameMode>.Instance.session.roundState);
                Logger.LogInfo(Singleton<BaseGameMode>.Instance.session.roundState);
            }
            if (RestartKey.Value.IsDown())
            {
                Singleton<RestartPacketHandler>.Instance.Send();
            }
        }

        void OnDestroy()
        {
            Plugin.Logger.LogInfo("Unload");

            Logger = null;

            while (patches.Count > 0)
            {
                patches.Pop().Disable();
            }

            ragdollCreator.Dispose();
            ragdollCreator = null;


            // Packet Handlers
            Singleton<PlayerKilledPacketHandler>.Instance.Dispose();
            Singleton<BombStatePacketHandler>.Instance.Dispose();

            Singleton<SessionInfoPacketHandler>.Instance.Dispose();
            Singleton<RestartPacketHandler>.Instance.Dispose();
            Singleton<RoundStatePacketHandler>.Instance.Dispose();

            Singleton<BaseGameMode>.Instance.Dispose();
        }
    }
}
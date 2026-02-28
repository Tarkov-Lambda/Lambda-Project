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
using SPT.Reflection;
using SPT.Reflection.Patching;
using System;
using UnityEngine;

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

        RagdollCreator ragdollCreator;

        void Start()
        {
            Logger = base.Logger;
            Plugin.Logger.LogInfo("Load");

            jopa = new Patch_Gameworld_OnGameStarted();
            jopa.Enable();

            patchKill = new pActiveHealthController_Kill();
            patchKill.Enable();

            canWalk = new Patch_CanWalk();
            canWalk.Enable();

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
                Singleton<BaseGameMode>.Instance.sessionInfo.roundState = (RoundState)(((int)Singleton<BaseGameMode>.Instance.sessionInfo.roundState + 1) % 5); ;
                Singleton<RoundStatePacketHandler>.Instance.Send(Singleton<BaseGameMode>.Instance.sessionInfo.roundState);
                Logger.LogInfo(Singleton<BaseGameMode>.Instance.sessionInfo.roundState == RoundState.Warmup);
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

            patchKill.Disable();
            jopa.Disable();
            canWalk.Disable();

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
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Dissonance;
using Fika.Core.Networking.LiteNetLib.Utils;
using HarmonyLib;
using ifp.arena.bep.GameTypes;
using ifp.arena.shared;
using SPT.Reflection;
using System;

namespace ifp.arena.bep
{
    [BepInPlugin("com.ifp.respawn", MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public static new ManualLogSource Logger;

        internal static ConfigEntry<bool> Active;
        internal static ConfigEntry<GameModes> GameMode;
        internal static ConfigEntry<Faction> PrefferedFaction;

        public static BaseGameMode gameSession = new BaseGameMode();

        //Patch_Fika_OnCommonPlayerPacketReceived packetPatch;
        //Patch_FikaClient_OnCommonPlayerPacketReceived packetPatch2;
        pActiveHealthController_Kill patchKill;

        void Start()
        {
            Logger = base.Logger;
            Plugin.Logger.LogInfo("Load");


            //packetPatch = new Patch_Fika_OnCommonPlayerPacketReceived();
            //packetPatch.Enable();

            //packetPatch2 = new Patch_FikaClient_OnCommonPlayerPacketReceived();
            //packetPatch2.Enable();

            patchKill = new pActiveHealthController_Kill();
            patchKill.Enable();

            InitConfiguration();
        }

        private void InitConfiguration()
        {
            Active = Config.Bind("", "Active", true, "");
            GameMode = Config.Bind("", "Gamemodes", GameModes.FFA, "");
            PrefferedFaction = Config.Bind("", "Preffered Faction", Faction.None, "");
        }

        void OnDestroy()
        {
            Plugin.Logger.LogInfo("Unload");
            //packetPatch.Disable();
            //packetPatch2.Disable();
            patchKill.Disable();
        }
    }
}
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Dissonance;
using HarmonyLib;
using SPT.Reflection;

namespace ifp.arena.respawn
{
    [BepInPlugin("com.ifp.respawn", MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public static new ManualLogSource Logger;

        internal static ConfigEntry<bool> Active;
        internal static ConfigEntry<Gamemodes> Gamemodes;

        Patch_Fika_OnCommonPlayerPacketReceived packetPatch;
        Patch_FikaClient_OnCommonPlayerPacketReceived packetPatch2;
        pActiveHealthController_Kill patchKill;

        void Start()
        {
            Logger = base.Logger;
            Plugin.Logger.LogInfo("Load");


            packetPatch = new Patch_Fika_OnCommonPlayerPacketReceived();
            packetPatch.Enable();

            packetPatch2 = new Patch_FikaClient_OnCommonPlayerPacketReceived();
            packetPatch2.Enable();

            patchKill = new pActiveHealthController_Kill();
            patchKill.Enable();

            InitConfiguration();
        }

        private void InitConfiguration()
        {
            Active = Config.Bind("", "Active", true, "Works only on Server");
            Gamemodes = Config.Bind("", "Gamemodes", respawn.Gamemodes.FFA, "Works only on Server");
        }

        void OnDestroy()
        {
            Plugin.Logger.LogInfo("Unload");
            packetPatch.Disable();
            packetPatch2.Disable();
            patchKill.Disable();
        }
    }
}
using BepInEx;
using BepInEx.Logging;
using UnityEngine;

namespace ifp.tracer
{
    [BepInPlugin("com.ifp.tracer", MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    internal class Plugin : BaseUnityPlugin
    {
        public static new ManualLogSource Logger { get; private set; }


        void Start()
        {
            Logger = base.Logger;

            GameObject tracerOverlay = new GameObject("Tracer Overlay");
            tracerOverlay.AddComponent<TracerOverlay>();
            DontDestroyOnLoad(tracerOverlay);
        }

        void OnDestroy()
        {
            Logger = null;
        }
    }
}

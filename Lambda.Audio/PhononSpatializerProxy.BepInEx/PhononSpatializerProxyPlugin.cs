using BepInEx;
using HarmonyLib;
using Lambda.Audio.SteamIntegration;
using System.IO;
using UnityEngine;

namespace PhononSpatializerProxy.BepInEx;

[BepInPlugin("com.ifp.PhononSpatializerProxy", MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class PhononSpatializerProxyPlugin : BaseUnityPlugin
{
    public static readonly string pathToBinaries = Path.Combine(global::BepInEx.Paths.PluginPath, "ifp", "Binaries");

    private Harmony _harmony;

    void Start()
    {
        Logger.LogInfo("Loading PhononSpatializerProxyPlugin");
        
        // _harmony = new Harmony("com.ifp.PhononSpatializerProxy");
        // _harmony.PatchAll(Assembly.GetExecutingAssembly());

        // SteamAudioInitializer.Initialize();

        // PhononUpdateManager.Initialize();
    }

    void OnDestroy()
    {
        Logger.LogInfo("Unloading PhononSpatializerProxyPlugin");

        PhononUpdateManager.Instance.Dispose();

        _harmony.UnpatchSelf();
    }
}

internal static class GameObjectExtensions
{
    public static T GetOrAddComponent<T>(this GameObject gameObject) where T : Component
    {
        T typeComponent = gameObject.GetComponent<T>() ?? gameObject.AddComponent<T>();
        return typeComponent;
    }
}

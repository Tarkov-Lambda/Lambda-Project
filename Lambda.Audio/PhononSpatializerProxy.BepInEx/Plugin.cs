using BepInEx;
using HarmonyLib;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace PhononSpatializerProxy.BepInEx;

[BepInPlugin("com.ifp.PhononSpatializerProxy", MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    public static readonly string pathToBinaries = Path.Combine(global::BepInEx.Paths.PluginPath, "ifp", "Binaries");

    private Harmony _harmony;

    void Start()
    {
        SteamAudioInitializer.Initialize();

        _harmony = new Harmony("com.ifp.PhononSpatializerProxy");
        _harmony.PatchAll(Assembly.GetExecutingAssembly());
    }

    void OnDestroy()
    {
        Logger.LogInfo("Unload");

        SteamAudioSourceController.Dispose();

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

using BepInEx;
using BepInEx.Logging;
using Comfort.Common;
using Cysharp.Threading.Tasks;
using ifp.arena.bep.networking;
using MemoryPack;
using SPT.Reflection;
using System;
using System.Collections.Generic;
using UnityEngine.LowLevel;


[BepInDependency("com.fika.core", BepInDependency.DependencyFlags.SoftDependency)]
[BepInPlugin("com.ifp.PacketHandler", MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
internal class Plugin : BaseUnityPlugin
{
    // internal static new ManualLogSource Logger => Logger;

    private readonly List<IDisposable> _disposables = new();
    private readonly List<Action> _releases = new();

    public static INetworkBackend Network { get; private set; }

    private void RegisterMemoryPackFormatter<T>(IMemoryPackFormatter<T> formatter)
    {
        if (!MemoryPackFormatterProvider.IsRegistered<T>())
        {
            MemoryPackFormatterProvider.Register<T>(formatter as MemoryPackFormatter<T>);
        }
    }

    void Start()
    {
        if (BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("com.fika.core"))
        {
            InitFikaBackend();
        }
        else
        {
            Network = new LocalSPBackend();
            Logger.LogInfo("Fika not found. PacketHandler running in Local SP Mode.");
        }

        // Logger.LogInfo("PacketHandler is loading");
        PlayerLoopSystem playerLoop = PlayerLoop.GetCurrentPlayerLoop();
        PlayerLoopHelper.Initialize(ref playerLoop);

        RegisterMemoryPackFormatter(new PlayerFormatter());                         // Player -> Profile ID
        RegisterMemoryPackFormatter(new ItemFormatter());                           // Item -> Binary via EFT internals
        RegisterMemoryPackFormatter(new InventoryDescriptorFormatter());                           // Item -> Binary via EFT internals
    }

    // CRITICAL: MethodImplOptions.NoInlining prevents the JIT compiler from 
    // inspecting FikaBackend until we are 100% sure Fika exists. 
    // If you don't do this, the plugin will crash on Awake() before the if statement even runs.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void InitFikaBackend()
    {
        Network = new FikaBackend();
        Logger.LogInfo("Fika detected. PacketHandler running in MP Mode.");
    }

    private void Update()
    {
    }

    void OnDestroy()
    {
        Logger.LogInfo("PacketHandler is unloading");

        foreach (var disposable in _disposables)
            disposable.Dispose();

        _disposables.Clear();

        // Release concrete singleton slots AFTER all Dispose() calls so that
        // singletons can still safely access each other's Instance during teardown.
        foreach (var release in _releases)
            release();

        _releases.Clear();
    }
}

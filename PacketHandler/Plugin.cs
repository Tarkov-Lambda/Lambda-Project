using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using Comfort.Common;
using Cysharp.Threading.Tasks;
using MemoryPack;
using PacketHandler.TimeSync;
using SPT.Reflection;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine.LowLevel;


[BepInDependency("com.fika.core", BepInDependency.DependencyFlags.SoftDependency)]
[BepInPlugin("com.ifp.PacketHandler", MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
internal class Plugin : BaseUnityPlugin
{
    internal static new ManualLogSource Logger = null;

    private readonly List<IDisposable> _disposables = new();
    private readonly List<Action> _releases = new();

    public static INetworkBackend Network { get; private set; }

    public readonly TimeSyncTicker Ticker = new();

    private void RegisterMemoryPackFormatter<T>(IMemoryPackFormatter<T> formatter)
    {
        if (!MemoryPackFormatterProvider.IsRegistered<T>())
        {
            MemoryPackFormatterProvider.Register<T>(formatter as MemoryPackFormatter<T>);
        }
    }

    private void RegisterSingleton<T>() where T : class, IDisposable, new()
    {
        Logger.LogInfo($"Registering {typeof(T).Name}");
        var instance = new T();
        Singleton<T>.Create(instance);
        _disposables.Add(instance);
        _releases.Add(() => Singleton<T>.Release(instance));
    }

    void Start()
    {
        Logger = base.Logger;

        Logger.LogInfo("PacketHandler is loading");
        PlayerLoopSystem playerLoop = PlayerLoop.GetCurrentPlayerLoop();
        PlayerLoopHelper.Initialize(ref playerLoop);

        if (Chainloader.PluginInfos.ContainsKey("com.fika.core"))
        {
            // Player -> int id
            RegisterMemoryPackFormatter(new PlayerFormatter());
            RegisterMemoryPackFormatter(new ItemFormatter());
            RegisterMemoryPackFormatter(new InventoryDescriptorClassFormatter());
            RegisterMemoryPackFormatter(new ItemAddressFormatter());
            
            InitFikaBackend();

            RegisterSingleton<TimeSynchronizationPacketHandler>();
            RegisterSingleton<TimeSyncResponsePacketHandler>();
        }
        else
        {
            Network = new LocalBackend();
            Logger.LogInfo("Fika not found. PacketHandler running in Local SP Mode.");
        }

        RegisterSingleton<TestPacketHandler>();
        Singleton<TestPacketHandler>.Instance.Send();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void InitFikaBackend()
    {
        Network = new FikaBackend();
        Logger.LogInfo("Fika detected. PacketHandler running in MP Mode.");
    }

    private void Update() => Ticker.Update();

    void OnDestroy()
    {
        Logger.LogInfo("PacketHandler is unloading");

        Ticker.Dispose();

        foreach (var disposable in _disposables)
            disposable.Dispose();

        _disposables.Clear();

        foreach (var release in _releases)
            release();

        _releases.Clear();
    }
}

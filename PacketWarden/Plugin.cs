using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using Comfort.Common;
using Cysharp.Threading.Tasks;
using MemoryPack;
using PacketWarden.TimeSync;
using SPT.Reflection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine.LowLevel;


[BepInDependency("com.fika.core", BepInDependency.DependencyFlags.SoftDependency)]
[BepInPlugin("com.ifp.PacketWarden", MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
internal class Plugin : BaseUnityPlugin
{
    public static readonly string pathToPacketWarden = Path.Combine(BepInEx.Paths.PluginPath, "ifp");

    internal static new ManualLogSource Logger = null;

    private readonly List<IDisposable> _disposables = new();
    private readonly List<Action> _releases = new();

    public static INetworkBackend Network { get; private set; }

    public readonly TimeSyncTicker Ticker = new();

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
        Logger.LogInfo("PacketWarden is loading");

        PlayerLoopSystem playerLoop = PlayerLoop.GetCurrentPlayerLoop();
        PlayerLoopHelper.Initialize(ref playerLoop);

        MemoryPackFormatterProvider.Register(new PlayerFormatter());
        MemoryPackFormatterProvider.Register(new ItemFormatter());
        MemoryPackFormatterProvider.Register(new InventoryDescriptorClassFormatter());
        MemoryPackFormatterProvider.Register(new ItemAddressFormatter());

        if (Chainloader.PluginInfos.ContainsKey("com.fika.core"))
        {
            InitFikaBackend();
            RegisterSingleton<TimeSynchronizationPacketWarden>();
        }
        else
        {
            Network = new LocalBackend();
            Logger.LogInfo("PacketWarden running in Local Mode.");
        }

        H.OnNetworkCreated += NetworkTime.Reset;

        RegisterSingleton<TestPacketWarden>();
        Singleton<TestPacketWarden>.Instance.Send();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void InitFikaBackend()
    {
        try
        {
            string fikaDllPath = Path.Combine(pathToPacketWarden, "PacketWarden.FikaIntegration.dll");

            Assembly fikaAssembly = Assembly.LoadFrom(fikaDllPath);

            Type bootstrapType = fikaAssembly.GetType("PacketWarden.FikaIntegration.FikaBootstrap");
            MethodInfo initMethod = bootstrapType.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static);

            Network = (INetworkBackend)initMethod.Invoke(null, null);

            Logger.LogInfo("PacketWarden running in MP Mode.");
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to load Fika Integration: {ex}");
            Logger.LogError(ex.StackTrace);
            Network = new LocalBackend();
        }
    }

    private void Update() => Ticker.Update();

    void OnDestroy()
    {
        Logger.LogInfo("PacketWarden is unloading");

        foreach (var disposable in _disposables)
            disposable.Dispose();

        _disposables.Clear();

        foreach (var release in _releases)
            release();

        _releases.Clear();

        H.OnNetworkCreated -= NetworkTime.Reset;
    }
}

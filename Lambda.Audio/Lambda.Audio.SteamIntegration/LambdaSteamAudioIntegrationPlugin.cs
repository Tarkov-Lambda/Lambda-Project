using BepInEx;
using BepInEx.Logging;
using Comfort.Common;
using Cysharp.Threading.Tasks;
using EFT;
using Lambda.Audio.SteamIntegration.AudioRooms;
using Lambda.Audio.SteamIntegration.Patches;
using SPT.Reflection.Patching;
using SteamAudio;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEngine;

namespace Lambda.Audio.SteamIntegration;

// [BepInDependency("com.ifp.PhononSpatializerProxy")]
[BepInPlugin("com.Lambda.Audio.SteamAudioIntegration", MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class LambdaSteamAudioIntegrationPlugin : BaseUnityPlugin
{
    public static readonly string pathToBinaries = Path.Combine(BepInEx.Paths.PluginPath, "ifp", "Binaries");

    internal ManualLogSource Log => Logger;

    private readonly List<ModulePatch> _patches = new();
    private readonly List<IDisposable> _disposables = new();
    private readonly List<Action> _releases = new();

    private CancellationTokenSource _cts;

    public static bool IsInRaid()
    {
        var gameWorld = Singleton<GameWorld>.Instance;
        return gameWorld != null && gameWorld is not HideoutGameWorld;
    }

    public async UniTask RegisterSingletonInRaid<T>() where T : class, IDisposable, new()
    {
        try
        {
            await UniTask.WaitUntil(() => IsInRaid(), cancellationToken: _cts.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        RegisterSingleton<T>();
    }

    private void RegisterPatch(ModulePatch patch)
    {
        patch.Enable();
        _patches.Add(patch);
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
        Logger.LogInfo("Loading LambdaSteamAudioIntegrationPlugin");

        _cts = new CancellationTokenSource();

        if (PacketWardenUtils.Network.IsHeadless) return;

        RegisterPatch(new Patch_BetterAudio_SetProtagonist());                      // Attach SteamAudioListener to the local player's AudioListener transform whenever SetProtagonist is called (raid spawn).

        // RegisterPatch(new Patch_BetterAudio_FadeMixerVolume());

        // RegisterPatch(new Patch_BetterSource_CheckBinauralAllowed());               // Audio Source Routing

        RegisterPatch(new Patch_BetterSource_Init());                                   // do not let anything be occluded

        // foreach (BetterSource source in FindObjectsByType<BetterSource>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        // {
        //     BetterSourceProxyRouter.LobotomizeBetterSource(source);
        // }

        RegisterPatch(new Patch_BetterSource_SetOcclusionVolumeFactor());           // do not let anything be occluded
        RegisterPatch(new Patch_BetterSource_SetOcclusionRolloffScale());
        RegisterPatch(new Patch_SpatialLowPassFilter_CalculateFrequency());         // bypass low filter muffling
        RegisterPatch(new Patch_SpatialHighPassFilter_CalculateFrequency());        // bypass high filter muffling

        RegisterPatch(new Patch_BetterSource_SetLowPassFilterParameters());         //
        RegisterPatch(new Patch_BetterSource_SetHighPassFilterParameters());        //

        RegisterPatch(new Patch_SpatialAudioSystem_Update());                       // bypass high filter muffling
        RegisterPatch(new Patch_SpatialAudioSystem_LateUpdate());                   // bypass high filter muffling

        RegisterPatch(new Patch_SpatialAudioSystem_ListenerCurrentRoom());          // force audio room to always be Phantom Audio Room
        RegisterPatch(new Patch_SpatialAudioSystem_ProcessSourceOcclusion_1());     // bypass occlusion containers
        RegisterPatch(new Patch_SpatialAudioSystem_ProcessSourceOcclusion_2());     // bypass occlusion containers
        RegisterPatch(new Patch_SpatialAudioSystem_ProcessSourceOcclusion_3());     // bypass occlusion containers

        RegisterSingletonInRaid<LambdaAudioRoomController>().Forget();              // We invoke all audio room changes manually

        // RegisterSingleton<BetterSourceProxyRouter>();
    }

    void OnDestroy()
    {
        Logger.LogInfo("Unloading LambdaSteamAudioIntegrationPlugin");

        if (PacketWardenUtils.Network.IsHeadless) return;

        foreach (var patch in _patches)
        {
            if (patch is IDisposable disposablePatch)
                disposablePatch.Dispose();

            patch.Disable();
        }

        _patches.Clear();

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
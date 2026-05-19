using BepInEx;
using Comfort.Common;
using Cysharp.Threading.Tasks;
using EFT;
using PhononSpatializerProxy.BepInEx.Patches;
using SPT.Reflection.Patching;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace PhononSpatializerProxy.BepInEx;

[BepInPlugin("com.Lambda.Audio.PhononSpatializerProxy", MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    public static readonly string pathToBinaries = Path.Combine(global::BepInEx.Paths.PluginPath, "ifp", "Binaries");

    private readonly List<ModulePatch> _patches = new();
    private readonly List<IDisposable> _disposables = new();
    private readonly List<Action> _releases = new();

    private void RegisterPatch(ModulePatch patch)
    {
        patch.Enable();
        _patches.Add(patch);
    }

    void Start()
    {
        SteamAudioInitializer.Initialize();

        RegisterPatch(new Patch_AudioSource_set_spatialize());                      // Force internal spatialization off and redirect the real value to the DSP bridge
        RegisterPatch(new Patch_AudioSource_get_spatialize());                      // Force internal spatialization off and redirect the real value to the DSP bridge
        RegisterPatch(new Patch_AudioSource_set_spatialBlend());                    // Proxy spatialBlend calls to PhononDSPBridge
        RegisterPatch(new Patch_AudioSource_get_spatialBlend());                    // Proxy spatialBlend calls to PhononDSPBridge
    }

    void OnDestroy()
    {
        Logger.LogInfo("Unload");

        SteamAudioSourceController.Dispose();

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

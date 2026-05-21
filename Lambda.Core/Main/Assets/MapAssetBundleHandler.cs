using Comfort.Common;
using Cysharp.Threading.Tasks;
using Lambda.Core.Networking;
using Lambda.Core.Patches.Tarkov;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lambda.Core.Main.AssetBundleHandling;

public class MapAssetBundleLoader : Singleton<MapAssetBundleLoader>, IDisposable
{
    private readonly Dictionary<string, AssetBundle> loadedAssetBundles = new();

    public MapAssetBundleLoader()
    {
        Patch_Gameworld_OnDispose.OnDispose += UnloadEverythingOnGameWorldDispose;
    }

    public void Dispose()
    {
        Patch_Gameworld_OnDispose.OnDispose -= UnloadEverythingOnGameWorldDispose;
        UnloadAll(true);
        Release(this);
    }

    public async UniTask ReloadMap(string mapName)
    {
        D.Log($"[AssetBundleHandler] Hot Reloading Map: {mapName}");

        Vector3 originalPlayerPos = H.MainPlayer.Position;

        await UnloadMap(mapName);
        await LoadMap(mapName);

        H.MainPlayer.Teleport(originalPlayerPos + new Vector3(0f, 0.5f, 0f));
    }

    public async UniTask UnloadMap(string mapName)
    {
        string fullPath = Path.Combine(Plugin.pathToMaps, mapName);

        if (loadedAssetBundles.TryGetValue(fullPath, out AssetBundle bundle) && bundle != null)
        {
            var unloadTasks = new List<UniTask>();

            // Unload all active scenes tied to this bundle
            foreach (var scenePath in bundle.GetAllScenePaths())
            {
                if (SceneManager.GetSceneByPath(scenePath).isLoaded)
                {
                    unloadTasks.Add(SceneManager.UnloadSceneAsync(scenePath).ToUniTask());
                }
            }

            if (unloadTasks.Count > 0)
            {
                await UniTask.WhenAll(unloadTasks);
            }

            bundle.Unload(true);
        }

        loadedAssetBundles.Remove(fullPath);
    }

    public async UniTask LoadMap(string mapName)
    {
        if (mapName != "lobby")
            MapLoadEvent.OnBeginLoad?.Invoke();

        AssetBundle bundle = await LoadAssetBundle(mapName);
        if (bundle == null) return;

        string[] scenePaths = bundle.GetAllScenePaths();
        if (scenePaths.Length == 0)
        {
            D.LogError($"[AssetBundleHandler] Loaded Asset Bundle \"{mapName}\" does not have any scenes to load");
            return;
        }

        BundleLoadingProgressReport progressReportScene = new();
        var loadTasks = new List<UniTask>();

        foreach (var scenePath in scenePaths)
        {
            if (!SceneManager.GetSceneByPath(scenePath).isLoaded)
            {
                if (mapName == "lobby")
                {
                    bool isBunkerAlreadyLoaded = SceneManager.GetSceneByBuildIndex(131).isLoaded;
                    if (!isBunkerAlreadyLoaded)
                    {
                        UniTask reserveBunkerLoadTask = SceneManager.LoadSceneAsync(131, LoadSceneMode.Additive).ToUniTask();
                        await reserveBunkerLoadTask;
                    }
                }

                UniTask sceneLoadTask = SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive).ToUniTask(progressReportScene);
                loadTasks.Add(sceneLoadTask);
            }
        }

        await UniTask.WhenAll(loadTasks);

        if (H.GameWorld is HideoutGameWorld && mapName != "lobby")
        {
            foreach (var scenePath in scenePaths)
            {
                Scene scene = SceneManager.GetSceneByPath(scenePath);
                if (!scene.isLoaded) continue;

                Vector3 offset = GetOffsetForScene(scenePath);

                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    root.transform.position += offset;
                }
            }
        }

        await UniTask.DelayFrame(1);
        AmbientLight.RuntimeOptimizePrepare();

        if (mapName != "lobby")
            MapLoadEvent.OnSuccessfulLoad?.Invoke();
    }

    private Vector3 GetOffsetForScene(string scenePath)
    {
        return new Vector3(0, -330, 0);
    }

    public async UniTask<AssetBundle> LoadAssetBundle(string name)
    {
        string fullPath = Path.Combine(Plugin.pathToMaps, name);
        if (!File.Exists(fullPath))
        {
            D.LogError($"[AssetBundleHandler] Map file does not exist at: {fullPath}");
            return null;
        }

        // Check if it's already loaded, OR if it was previously cached as null
        if (!loadedAssetBundles.TryGetValue(fullPath, out AssetBundle bundle) || bundle == null)
        {
            BundleLoadingProgressReport progressReportBundle = new();

            bundle = await AssetBundle.LoadFromFileAsync(fullPath).ToUniTask(progressReportBundle);

            if (bundle == null)
            {
                D.Log($"[AssetBundleHandler] Failed to load AssetBundle '{name}'.");

                // Clean up the dictionary so we don't permanently cache a null failure
                loadedAssetBundles.Remove(fullPath);
                return null;
            }

            loadedAssetBundles[fullPath] = bundle;
        }

        return bundle;
    }

    void UnloadEverythingOnGameWorldDispose()
    {
        UnloadAll(true);
    }

    public void UnloadAll(bool includingLobby = false)
    {
        MapLoadEvent.OnBeginUnload?.Invoke();

        List<string> keysToRemove = new();

        foreach (var kvp in loadedAssetBundles)
        {
            AssetBundle bundle = kvp.Value;
            if (bundle != null)
            {
                // we do not unload lobby so that we can teleport there during reloads
                if (!includingLobby && bundle.name == "lobby") continue;

                foreach (var scenePath in bundle.GetAllScenePaths())
                {
                    if (SceneManager.GetSceneByPath(scenePath).isLoaded)
                    {
                        SceneManager.UnloadScene(scenePath); // synchronous unloading because of ScriptEngine hotreloading
                    }
                }

                bundle.Unload(true);
                keysToRemove.Add(kvp.Key);
            }
        }

        // Fix: Only remove what we actually unloaded so the Lobby isn't orphaned in native memory
        foreach (string key in keysToRemove)
        {
            loadedAssetBundles.Remove(key);
        }

        MapLoadEvent.OnUnload?.Invoke();
    }
}

class BundleLoadingProgressReport : IProgress<float>
{
    public float CurrentProgress { get; private set; }

    public void Report(float value)
    {
        CurrentProgress = value;

        if (D.TryEnterThrottle("BundleLoadingProgressReport", 7500))
        {
            D.Notify($"Loading Progress: {value * 100}%");
            Singleton<LoadProgressPacketWarden>.Instance.Send(value);
        }
    }
}
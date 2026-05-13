using Comfort.Common;
using Cysharp.Threading.Tasks;
using ifp.arena.bep.networking;
using ifp.arena.bep.Patches.Tarkov;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ifp.arena.bep.Core.AssetBundleHandling;

public class MapAssetBundleHandler : Singleton<MapAssetBundleHandler>, IDisposable
{
    private readonly Dictionary<string, AssetBundle> loadedAssetBundles = [];

    public MapAssetBundleHandler()
    {
        Patch_Gameworld_OnDispose.OnDispose += UnloadEverythingOnGameWorldDispose;
    }

    public async UniTask LoadMap(string mapName)
    {
        if (mapName != "lobby")
            MapLoadEvent.OnBeginLoad?.Invoke();

        AssetBundle bundle = await LoadAssetBundle(mapName);
        if (bundle == null) return;

        string[] scenePaths = bundle.GetAllScenePaths();
        if (scenePaths.Length == 0) D.LogError($"[AssetBundleHandler] Loaded Asset Bundle \"{mapName}\" does not have any scenes to load");

        BundleLoadingProgressReport progressReportScene = new BundleLoadingProgressReport();

        // Unloading in case it's already loaded (essentially to refresh for dev)
        // also making sure it's not the lobby, because it's a persistent player safety place
        if (scenePaths[0] != "lobby")
        {
            var unloadTasks = new List<UniTask>();

            foreach (var scenePath in scenePaths)
            {
                if (SceneManager.GetSceneByPath(scenePath).isLoaded)
                {
                    unloadTasks.Add(SceneManager.UnloadSceneAsync(scenePath).ToUniTask());
                }
            }

            await UniTask.WhenAll(unloadTasks);
        }

        var loadTasks = new List<UniTask>();
        foreach (var scenePath in scenePaths)
        {
            if (!SceneManager.GetSceneByPath(scenePath).isLoaded)
            {
                // типо наверное надо авайт загрузить перед тем как ебашить аддитив ремувал?
                // в любом случае в будущем это будет ебашиться на заднем плане во время загрузки
                if (mapName == "lobby")
                {
                    bool isBunkerAlreadyLoaded = SceneManager.GetSceneByBuildIndex(131).isLoaded;
                    if (!isBunkerAlreadyLoaded)
                    {
                        UniTask reserveBunkerLoadTask = SceneManager.LoadSceneAsync(131, LoadSceneMode.Additive).ToUniTask();
                        // loadTasks.Add(reserveBunkerLoadTask);

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

                kvp.Value.Unload(true);
            }
        }

        loadedAssetBundles.Clear();
        MapLoadEvent.OnUnload?.Invoke();
    }

    public void Dispose()
    {
        Patch_Gameworld_OnDispose.OnDispose -= UnloadEverythingOnGameWorldDispose;
        UnloadAll();
        Release(this);
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
            Singleton<LoadProgressPacketHandler>.Instance.Send(value);
        }
    }
}

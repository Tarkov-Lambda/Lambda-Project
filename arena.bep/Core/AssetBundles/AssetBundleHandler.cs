using Comfort.Common;
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ifp.arena.bep.Core.AssetBundleHandling
{
    public class AssetBundleHandler : Singleton<AssetBundleHandler>, IDisposable
    {
        public static readonly string pathToBundlesDir = Path.Combine(BepInEx.Paths.PluginPath, "ifp", "bundles");
        private readonly Dictionary<string, AssetBundle> loadedAssetBundles = new Dictionary<string, AssetBundle>();

        public async UniTask LoadMap(string mapName)
        {
            AssetBundle MapBundle = await LoadAssetBundle(mapName);
            if (MapBundle == null) return;

            string[] scenePaths = MapBundle.GetAllScenePaths();
            if (scenePaths.Length == 0) H.LogError($"[AssetBundleHandler] Loaded Asset Bundle \"{mapName}\" does not have any scenes to load");

            BundleLoadingProgressReport progressReportScene = new BundleLoadingProgressReport();

            // Unloading in case it's already loaded (essentially to refresh for dev)
            // also making sure it's not the lobby, because it's a persistent player safety place
            if (SceneManager.GetSceneByPath(scenePaths[0]).isLoaded && scenePaths[0] != "lobby")
            {
                await SceneManager.UnloadSceneAsync(scenePaths[0]).ToUniTask();
            }

            if (!SceneManager.GetSceneByPath(scenePaths[0]).isLoaded)
            {
                await SceneManager.LoadSceneAsync(scenePaths[0], LoadSceneMode.Additive).ToUniTask(progressReportScene);
            }
        }

        public async UniTask<AssetBundle> LoadAssetBundle(string name)
        {
            string fullPath = Path.Combine(pathToBundlesDir, name);
            if (!File.Exists(fullPath))
            {
                H.LogError($"[AssetBundleHandler] Map file does not exist at: {fullPath}");
                return null;
            }

            // Check if it's already loaded, OR if it was previously cached as null
            if (!loadedAssetBundles.TryGetValue(fullPath, out AssetBundle bundle) || bundle == null)
            {

                BundleLoadingProgressReport progressReportBundle = new BundleLoadingProgressReport();

                bundle = await AssetBundle.LoadFromFileAsync(fullPath).ToUniTask(progressReportBundle);

                if (bundle == null)
                {
                    H.Log($"[AssetBundleHandler] Failed to load AssetBundle '{name}'.");

                    // Clean up the dictionary so we don't permanently cache a null failure
                    loadedAssetBundles.Remove(fullPath);
                    return null;
                }

                loadedAssetBundles[fullPath] = bundle;
            }

            return bundle;
        }

        void UnloadAll()
        {
            foreach (var kvp in loadedAssetBundles)
            {
                AssetBundle bundle = kvp.Value;
                if (bundle != null)
                {
                    // we do not unload lobby so that we can teleport there during reloads
                    if (bundle.name == "lobby") continue;

                    foreach (var scenePath in bundle.GetAllScenePaths())
                    {
                        if (SceneManager.GetSceneByPath(scenePath).isLoaded)
                        {
                            SceneManager.UnloadSceneAsync(scenePath);
                        }
                    }

                    kvp.Value.Unload(true);
                }
            }

            loadedAssetBundles.Clear();
        }

        public void Dispose()
        {
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
            // Optional: H.Log($"Loading Progress: {value * 100}%");
        }
    }
}
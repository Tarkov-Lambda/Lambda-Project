using Comfort.Common;
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ifp.arena.bep.Core.AssetBundleHandling
{
    internal class AssetBundleHandler : Singleton<AssetBundleHandler>, IDisposable
    {
        private readonly string pathToBundlesDir = Path.Combine(BepInEx.Paths.PluginPath, "ifp", "bundles");
        private readonly Dictionary<string, AssetBundle> loadedAssetBundles = new Dictionary<string, AssetBundle>();

        public async UniTask LoadMap(string mapName)
        {
            try
            {
                string fullPath = Path.Combine(pathToBundlesDir, mapName);

                if (!File.Exists(fullPath))
                {
                    Debug.LogError($"[AssetBundleHandler] Map file does not exist at: {fullPath}");
                    return;
                }

                // Check if it's already loaded, OR if it was previously cached as null
                if (!loadedAssetBundles.TryGetValue(fullPath, out AssetBundle bundle) || bundle == null)
                {
                    BundleLoadingProgressReport progressReportBundle = new BundleLoadingProgressReport();

                    bundle = await AssetBundle.LoadFromFileAsync(fullPath).ToUniTask(progressReportBundle);

                    if (bundle == null)
                    {
                        Debug.LogError($"[AssetBundleHandler] Failed to load AssetBundle '{mapName}'. It might be corrupted, built for the wrong platform, or loaded elsewhere.");

                        // Clean up the dictionary so we don't permanently cache a null failure
                        loadedAssetBundles.Remove(fullPath);
                        return;
                    }

                    loadedAssetBundles[fullPath] = bundle;
                }

                // Safely check if the bundle actually contains scenes
                string[] scenePaths = bundle.GetAllScenePaths();
                if (scenePaths.Length == 0)
                {
                    Debug.LogError($"[AssetBundleHandler] The AssetBundle '{mapName}' does not contain any Unity Scenes! Did you pack a prefab by mistake?");
                    return;
                }

                BundleLoadingProgressReport progressReportScene = new BundleLoadingProgressReport();

                NotificationManagerClass.DisplayMessageNotification($"{scenePaths.ToString()}");

                if (SceneManager.GetSceneByPath(scenePaths[0]).isLoaded)
                    return;

                // Explicitly define LoadSceneMode.Single (or Additive if you are layering maps)
                await SceneManager.LoadSceneAsync(scenePaths[0], LoadSceneMode.Additive).ToUniTask(progressReportScene);

                Debug.Log($"[AssetBundleHandler] Successfully loaded scene: {scenePaths[0]}");
            }
            catch (Exception ex)
            {
                // CRITICAL: This ensures any async crashes print directly to your BepInEx console
                Debug.LogError($"[AssetBundleHandler] Exception while loading map '{mapName}': {ex}");
            }
        }

        void UnloadAll()
        {
            foreach (var kvp in loadedAssetBundles)
            {
                if (kvp.Value != null)
                {
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
            // Optional: Debug.Log($"Loading Progress: {value * 100}%");
        }
    }
}
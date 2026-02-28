using Comfort.Common;
using Cysharp.Threading.Tasks;
using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
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
            string fullPath = Path.Combine(pathToBundlesDir, mapName);

            if (!File.Exists(fullPath))
                return;

            if (!loadedAssetBundles.ContainsKey(fullPath))
            {
                BundleLoadingProgressReport progressReportBundle = new BundleLoadingProgressReport();
                UniTask<AssetBundle> loadTask = AssetBundle.LoadFromFileAsync(Path.Combine(pathToBundlesDir, mapName)).ToUniTask(progressReportBundle);
                loadedAssetBundles[fullPath] = await loadTask;

                if (loadedAssetBundles[fullPath] == null)
                    return;
            }

            BundleLoadingProgressReport progressReportScene = new BundleLoadingProgressReport();
            await SceneManager.LoadSceneAsync(loadedAssetBundles[fullPath].GetAllScenePaths()[0]).ToUniTask(progressReportScene);
        }

        void UnloadAll()
        {
            foreach (var kvp in loadedAssetBundles)
            {
                kvp.Value.Unload(true);
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
        }
    }
}

using Comfort.Common;
using Cysharp.Threading.Tasks;
using ifp.arena.bep.Core.AssetBundleHandling;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ifp.arena.bep.Core.FX
{
    internal class FXHandler : Singleton<FXHandler>, IDisposable
    {
        private AssetBundle fxbundle;

        private GameObject prefabFire;

        public FXHandler()
        {
            fxbundle = AssetBundle.LoadFromFile(System.IO.Path.Combine(AssetBundleHandler.pathToBundlesDir, "fx"));
            prefabFire = fxbundle.LoadAsset<GameObject>("Assets/FX/FLAMES/Fire_zone_Animaton.prefab");
        }

        public Action SpawnMolotov(Vector3 pos)
        {
            GameObject instance = GameObject.Instantiate(prefabFire);
            instance.transform.position = pos;

            return () => instance.gameObject.SetActive(false);
        }

        public void Dispose()
        {
            fxbundle.Unload(false);
        }
    }
}

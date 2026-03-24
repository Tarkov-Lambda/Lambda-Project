using Comfort.Common;
using ifp.arena.bep.Core.AssetBundleHandling;
using ifp.arena.shared.FX;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ifp.arena.bep.Core.FX
{
    internal class FXHandler : Singleton<FXHandler>, IDisposable
    {
        public AssetBundle fxbundle { get; private set; }
        private MolotovFXController prefabFire;

        private Stack<MolotovFXController> molotovPool = new Stack<MolotovFXController>();

        Transform parentEffects;

        public FXHandler()
        {
            fxbundle = AssetBundle.LoadFromFile(System.IO.Path.Combine(MapAssetBundleHandler.pathToBundlesDir, "fx"));

            prefabFire = fxbundle.LoadAsset<GameObject>("Assets/FX/FLAMES/MolotovFX.prefab").GetComponent<MolotovFXController>();

            parentEffects = new GameObject("FX").transform;
            GameObject.DontDestroyOnLoad(parentEffects.gameObject);
        }

        public MolotovFXController SpawnMolotov(Vector3 pos, float startRadius, float endRadius, float bloomDuration)
        {
            MolotovFXController instance;

            if (molotovPool.Count > 0)
            {
                instance = molotovPool.Pop();
                instance.gameObject.SetActive(true);
            }
            else
            {
                instance = GameObject.Instantiate(prefabFire, parentEffects);
            }

            instance.transform.position = pos;
            instance.transform.localScale = new Vector3(startRadius, 1f, startRadius);
            instance.Ignite(ReturnToPool);

            return instance;
        }

        private void ReturnToPool(MolotovFXController controller)
        {
            controller.gameObject.SetActive(false);
            molotovPool.Push(controller);
        }

        public void Dispose()
        {
            GameObject.Destroy(parentEffects.gameObject);

            fxbundle.Unload(false);
            molotovPool.Clear();
        }
    }
}

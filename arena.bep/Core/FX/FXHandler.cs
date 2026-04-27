using Comfort.Common;
using ifp.arena.bep.Core.AssetBundleHandling;
using ifp.arena.shared.FX;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ifp.arena.bep.Core.FX;

public class FXHandler : Singleton<FXHandler>, IDisposable
{
    private const string FX_BUNDLE_NAME = "fx";
    public readonly string MOLOTOV_FIRE_PREFAB_PATH = "Packages/com.ifp.arena.shared/FX/Molotov/MolotovFX.prefab";

    public string FXBundlePath => Path.Combine(Plugin.pathToBundles, FX_BUNDLE_NAME);

    public AssetBundle FXBundle;
    private MolotovFXController MolotovFirePrefab;

    private Stack<MolotovFXController> molotovPool = new Stack<MolotovFXController>();

    Transform parentEffects;

    public FXHandler()
    {
        H.OnGameStarted += Initialize;
        H.OnGameDispose += Dispose;

        if (H.IsInRaid()) Initialize();
    }

    public void Initialize()
    {
        FXBundle = AssetBundle.LoadFromFile(FXBundlePath);
        MolotovFirePrefab = FXBundle.LoadAsset<GameObject>(MOLOTOV_FIRE_PREFAB_PATH).GetComponent<MolotovFXController>();

        parentEffects = new GameObject("FX").transform;
        GameObject.DontDestroyOnLoad(parentEffects.gameObject);
    }

    public void Dispose()
    {
        if (parentEffects != null)
            GameObject.Destroy(parentEffects.gameObject);

        FXBundle.Unload(false);
        molotovPool.Clear();
        Release(this);
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
            instance = GameObject.Instantiate(MolotovFirePrefab, parentEffects);
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
}

using System;
using Comfort.Common;
using EFT;
using ifp.arena.bep.Core.AssetBundleHandling;
using ifp.arena.bep.Core.FX;
using ifp.arena.bep.Core.Gamemode;
using ifp.arena.bep.networking;
using ifp.arena.bep.Patches.Tarkov;
using ifp.arena.shared;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ifp.arena.bep.Core
{
    // public class RaymarchHandler : Singleton<RaymarchHandler>, IDisposable
    // {
    //     // public GameObject RaymarchHandlerObject { get; private set; }
    //     public Raymarcher Raymarcher { get; private set; }

    //     public Gun Gun { get; private set; }

    //     private AssetBundle fxbundle => Singleton<FXHandler>.Instance.fxbundle;
    //     private GameObject FPSCameraGameObject => CameraClass.Instance.Camera.gameObject;


    //     public RaymarchHandler()
    //     {
    //         Patch_Gameworld_OnGameStarted.OnGameStarted += OnGameStarted;
    //         Patch_Gameworld_OnDispose.OnDispose += OnGameDispose;
    //         MapAssetBundleHandler.OnSuccessfulLoad += TryEnableRaymarcher;
    //         MapAssetBundleHandler.OnBeginUnload += ClearRaymarcher; // MapAssetBundleHandler will call this on game dispose
    //         // GameModeTicker.onUpdate += OnUpdate;
    //         // GameModeTicker.onLateUpdate += OnLateUpdate;
    //     }

    //     public void Dispose()
    //     {
    //         // GameObject.DestroyImmediate(RaymarchHandlerObject);
    //         Patch_Gameworld_OnGameStarted.OnGameStarted -= OnGameStarted;
    //         Patch_Gameworld_OnDispose.OnDispose -= OnGameDispose;
    //         MapAssetBundleHandler.OnSuccessfulLoad -= TryEnableRaymarcher;
    //         MapAssetBundleHandler.OnBeginUnload -= ClearRaymarcher; // MapAssetBundleHandler will call this on game dispose
    //         // GameModeTicker.onUpdate -= OnUpdate;
    //         // GameModeTicker.onLateUpdate -= OnLateUpdate;
    //         Release(this);
    //     }

    //     private void OnUpdate()
    //     {

    //     }

    //     private void OnLateUpdate()
    //     {

    //     }

    //     private void OnGameStarted(GameWorld gWorld)
    //     {
    //         // var RaymarchHandlerPrefab = fxbundle.LoadAsset<GameObject>("Packages/com.ifp.arena.shared/FX/Smoke/Prefabs/RaymarcherHandler.prefab");
    //         // RaymarchHandlerObject = GameObject.Instantiate(RaymarchHandlerPrefab, CameraClass.Instance.Camera.transform);

    //         // RaymarchHandlerObject.GetComponent<Raymarcher>().cam = CameraClass.Instance.Camera;

    //         // RaymarchHandlerObject = new GameObject("VoxelHandlerObject");
    //         // RaymarchHandlerObject.SetActive(false);

    //         Raymarcher = FPSCameraGameObject.AddComponent<Raymarcher>();
    //         Raymarcher.enabled = false;

    //         Gun = FPSCameraGameObject.AddComponent<Gun>();
    //         Gun.enabled = false;

    //         Raymarcher.compositeMaterial = new Material(fxbundle.LoadAsset<Shader>("Packages/com.ifp.arena.shared/FX/Smoke/Shaders/CompositeEffects.shader"));
    //         Raymarcher.raymarchCompute = fxbundle.LoadAsset<ComputeShader>("Packages/com.ifp.arena.shared/FX/Smoke/Resources/RenderSmoke.compute");

    //         // RaymarchHandlerObject.SetActive(false);


    //         // UnityEngine.Object.DontDestroyOnLoad(RaymarchHandlerObject);
    //     }

    //     private void OnGameDispose(GameWorld gWorld)
    //     {

    //     }

    //     private void TryEnableRaymarcher()
    //     {
    //         Voxelizer localVoxelizer = GameObject.FindFirstObjectByType<Voxelizer>();

    //         if (localVoxelizer != null)
    //         {
    //             localVoxelizer.voxelizeCompute = fxbundle.LoadAsset<ComputeShader>("Packages/com.ifp.arena.shared/FX/Smoke/Resources/Voxelize.compute");
    //             Raymarcher.smokeVoxelData = localVoxelizer;
    //             Raymarcher.enabled = true;
    //             Gun.enabled = true;
    //             // RaymarchHandlerObject.SetActive(true);
    //         }
    //     }

    //     private void ClearRaymarcher()
    //     {
    //         // RaymarchHandlerObject.SetActive(false);
    //         Raymarcher.enabled = false;
    //         Gun.enabled = false;

    //         Raymarcher.GetComponent<Raymarcher>().smokeVoxelData = null;
    //     }

    // }
}
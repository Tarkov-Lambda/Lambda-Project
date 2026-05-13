using System;
using Comfort.Common;
using EFT;
using Lambda.Core.Main.AssetBundleHandling;
using Lambda.Core.Main.FX;
using Lambda.Core.Main.Gamemode;
using Lambda.Core.Networking;
using Lambda.Core.Patches.Tarkov;
using ifp.arena.shared;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lambda.Core.Main;

// public class RaymarchHandler : Singleton<RaymarchHandler>, IDisposable
// {
//     public GameObject RaymarchHandlerObject { get; private set; }
//     public Raymarcher Raymarcher { get; private set; }
//     public GameObject GunObject { get; private set; }
//     public Gun Gun { get; private set; }

//     private bool _raymarcherWasActive;
//     private Voxelizer _cachedVoxelizer;

//     private AssetBundle fxbundle => H.FXHandler.fxbundle;
//     private GameObject FPSCameraGameObject => CameraClass.Instance.Camera.gameObject;

//     public RaymarchHandler()
//     {
//         Patch_Gameworld_OnGameStarted.OnGameStarted += OnGameStarted;
//         Patch_Gameworld_OnDispose.OnDispose         += OnGameDispose;
//         MapAssetBundleHandler.OnSuccessfulLoad      += TryEnableRaymarcher;
//         MapAssetBundleHandler.OnBeginUnload         += ClearRaymarcher;
//     }

//     public void Dispose()
//     {
//         Patch_Gameworld_OnGameStarted.OnGameStarted -= OnGameStarted;
//         Patch_Gameworld_OnDispose.OnDispose         -= OnGameDispose;
//         MapAssetBundleHandler.OnSuccessfulLoad      -= TryEnableRaymarcher;
//         MapAssetBundleHandler.OnBeginUnload         -= ClearRaymarcher;

//         UnsubscribeCameraChanged();
//         Release(this);
//     }

//     private void OnGameStarted(GameWorld gWorld)
//     {
//         SetupOnCamera(FPSCameraGameObject);

//         CameraClass.Instance.OnCameraChanged += OnTarkovCameraChanged;
//     }

//     private void OnGameDispose(GameWorld gWorld)
//     {
//         GameObject.Destroy(GunObject);
//         UnsubscribeCameraChanged();
//         _raymarcherWasActive = false;
//         _cachedVoxelizer     = null;
//     }

//     private void OnTarkovCameraChanged()
//     {
//         SetupOnCamera(FPSCameraGameObject);

//         if (_raymarcherWasActive && _cachedVoxelizer != null)
//         {
//             Raymarcher.smokeVoxelData = _cachedVoxelizer;
//             Raymarcher.enabled        = true;
//             Gun.enabled               = true;
//         }
//     }

//     private void UnsubscribeCameraChanged()
//     {
//         if (CameraClass.Exist)
//             CameraClass.Instance.OnCameraChanged -= OnTarkovCameraChanged;
//     }

//     private void SetupOnCamera(GameObject cameraGO)
//     {
//         Raymarcher = cameraGO.AddComponent<Raymarcher>();
//         Raymarcher.compositeMaterial =
//             new Material(fxbundle.LoadAsset<Shader>(
//                 "Packages/com.ifp.arena.shared/FX/Smokes/Shaders/CompositeEffects.shader"));
//         Raymarcher.raymarchCompute =
//             fxbundle.LoadAsset<ComputeShader>(
//                 "Packages/com.ifp.arena.shared/FX/Smokes/Resources/RenderSmoke.compute");
//         Raymarcher.enabled = false;



//         GunObject = new GameObject("Gun");
//         Gun = GunObject.AddComponent<Gun>();
//         Raymarcher.gun = Gun;
//         Gun.enabled = false;
//     }

//     private void TryEnableRaymarcher()
//     {
//         Voxelizer localVoxelizer = GameObject.FindFirstObjectByType<Voxelizer>();

//         if (localVoxelizer != null)
//         {
//             localVoxelizer.voxelizeCompute =
//                 fxbundle.LoadAsset<ComputeShader>(
//                     "Packages/com.ifp.arena.shared/FX/Smokes/Resources/Voxelize.compute");

//             // localVoxelizer.cam = CameraClass.Instance.Camera;


//             Raymarcher.smokeVoxelData = localVoxelizer;
//             Raymarcher.enabled        = true;
//             Gun.enabled               = true;

//             _cachedVoxelizer     = localVoxelizer;
//             _raymarcherWasActive = true;
//         }
//     }

//     private void ClearRaymarcher()
//     {
//         Raymarcher.enabled        = false;
//         Gun.enabled               = false;
//         Raymarcher.smokeVoxelData = null;

//         _raymarcherWasActive = false;
//     }
// }

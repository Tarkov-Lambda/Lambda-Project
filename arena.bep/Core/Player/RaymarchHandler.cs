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
    public class RaymarchHandler : Singleton<RaymarchHandler>, IDisposable
    {
        public GameObject RaymarchHandlerObject { get; private set; }
        public Raymarcher Raymarcher { get; private set; }
        public GameObject GunObject { get; private set; }
        public Gun Gun { get; private set; }

        // Tracks whether the raymarcher was active when the camera last changed,
        // so we can re-enable it on the new camera automatically.
        private bool _raymarcherWasActive;
        private Voxelizer _cachedVoxelizer;

        private AssetBundle fxbundle => H.FXHandler.fxbundle;
        private GameObject FPSCameraGameObject => CameraClass.Instance.Camera.gameObject;

        // ── Lifecycle ────────────────────────────────────────────────────────

        public RaymarchHandler()
        {
            Patch_Gameworld_OnGameStarted.OnGameStarted += OnGameStarted;
            Patch_Gameworld_OnDispose.OnDispose         += OnGameDispose;
            MapAssetBundleHandler.OnSuccessfulLoad      += TryEnableRaymarcher;
            MapAssetBundleHandler.OnBeginUnload         += ClearRaymarcher;
        }

        public void Dispose()
        {
            Patch_Gameworld_OnGameStarted.OnGameStarted -= OnGameStarted;
            Patch_Gameworld_OnDispose.OnDispose         -= OnGameDispose;
            MapAssetBundleHandler.OnSuccessfulLoad      -= TryEnableRaymarcher;
            MapAssetBundleHandler.OnBeginUnload         -= ClearRaymarcher;

            UnsubscribeCameraChanged();
            Release(this);
        }

        // ── Game events ──────────────────────────────────────────────────────

        private void OnGameStarted(GameWorld gWorld)
        {
            SetupOnCamera(FPSCameraGameObject);

            // Listen for Tarkov rebuilding the FPS camera (map transitions,
            // settings changes) so we can re-attach to the new camera object.
            CameraClass.Instance.OnCameraChanged += OnTarkovCameraChanged;
        }

        private void OnGameDispose(GameWorld gWorld)
        {
            GameObject.Destroy(GunObject);
            UnsubscribeCameraChanged();
            _raymarcherWasActive = false;
            _cachedVoxelizer     = null;
        }

        // ── Camera-changed hook ──────────────────────────────────────────────

        private void OnTarkovCameraChanged()
        {
            // The old camera GameObject was destroyed by CameraClass.Reset().
            // Raymarcher and Gun components on it are gone — just re-add them
            // to the fresh camera.
            SetupOnCamera(FPSCameraGameObject);

            // If smoke was running before the camera swap, restore it.
            if (_raymarcherWasActive && _cachedVoxelizer != null)
            {
                Raymarcher.smokeVoxelData = _cachedVoxelizer;
                Raymarcher.enabled        = true;
                Gun.enabled               = true;
            }
        }

        private void UnsubscribeCameraChanged()
        {
            if (CameraClass.Exist)
                CameraClass.Instance.OnCameraChanged -= OnTarkovCameraChanged;
        }

        // ── Asset / component setup ──────────────────────────────────────────

        /// <summary>
        /// Attaches fresh Raymarcher and Gun components to <paramref name="cameraGO"/>
        /// and injects bundle-loaded assets. Both components are left disabled;
        /// call <see cref="TryEnableRaymarcher"/> (or enable them manually) to activate.
        /// </summary>
        private void SetupOnCamera(GameObject cameraGO)
        {
            Raymarcher = cameraGO.AddComponent<Raymarcher>();
            Raymarcher.compositeMaterial =
                new Material(fxbundle.LoadAsset<Shader>(
                    "Packages/com.ifp.arena.shared/FX/Smokes/Shaders/CompositeEffects.shader"));
            Raymarcher.raymarchCompute =
                fxbundle.LoadAsset<ComputeShader>(
                    "Packages/com.ifp.arena.shared/FX/Smokes/Resources/RenderSmoke.compute");
            Raymarcher.enabled = false;



            GunObject = new GameObject("Gun");
            Gun = GunObject.AddComponent<Gun>();
            Raymarcher.gun = Gun;
            Gun.enabled = false;
        }

        // ── Enable / disable ─────────────────────────────────────────────────

        private void TryEnableRaymarcher()
        {
            Voxelizer localVoxelizer = GameObject.FindFirstObjectByType<Voxelizer>();

            if (localVoxelizer != null)
            {
                localVoxelizer.voxelizeCompute =
                    fxbundle.LoadAsset<ComputeShader>(
                        "Packages/com.ifp.arena.shared/FX/Smokes/Resources/Voxelize.compute");
                D.Log(localVoxelizer.voxelizeCompute.name);
                localVoxelizer.cam = CameraClass.Instance.Camera;

                // Run the GPU buffer clears + kernel bindings that OnEnable
                // deferred because voxelizeCompute wasn't available yet.
                localVoxelizer.InitializeComputeDispatches();

                Raymarcher.smokeVoxelData = localVoxelizer;
                Raymarcher.enabled        = true;
                Gun.enabled               = true;

                _cachedVoxelizer     = localVoxelizer;
                _raymarcherWasActive = true;
            }
        }

        private void ClearRaymarcher()
        {
            Raymarcher.enabled        = false;
            Gun.enabled               = false;
            Raymarcher.smokeVoxelData = null;

            _raymarcherWasActive = false;
        }
    }
}

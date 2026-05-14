using HarmonyLib;
using SPT.Reflection.Patching;
using System;
using System.Reflection;
using UnityEngine;

namespace Lambda.Core.Main.UI;

internal class EFTCameraHook : IDisposable
{
    readonly ModulePatch _patch;

    internal EFTCameraHook()
    {
        Patch_CameraManager_SetCamera.OnPostfix += OnCameraChanged;
        if (CameraManager.Exist)
            OnCameraChanged(CameraManager.Instance);

        _patch = new Patch_CameraManager_SetCamera();
        _patch.Enable();
    }

    public void Dispose()
    {
        _patch.Disable();
        Patch_CameraManager_SetCamera.OnPostfix -= OnCameraChanged;

        if (CameraManager.Exist && CameraManager.Instance.Camera != null)
        {
            if (CameraManager.Instance.Camera.TryGetComponent<EFTScreenGrabber>(out var component))
            {
                Component.Destroy(component);
            }
        }
    }

    void OnCameraChanged(CameraManager cameraManager)
    {
        if (cameraManager.Camera == null)
            return;

        cameraManager.Camera.gameObject.GetOrAddComponent<EFTScreenGrabber>();
    }

    private class Patch_CameraManager_SetCamera : ModulePatch
    {
        public static event Action<CameraManager> OnPostfix;

        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(CameraManager), nameof(CameraManager.SetCamera));
        }

        [PatchPostfix]
        private static void PatchPostfix(CameraManager __instance, Camera camera)
        {
            OnPostfix?.Invoke(__instance);
        }
    }
}


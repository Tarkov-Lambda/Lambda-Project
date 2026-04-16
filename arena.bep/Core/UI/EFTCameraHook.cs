using System;
using UnityEngine;

namespace ifp.arena.bep.Core.UI;

internal class EFTCameraHook : IDisposable
{
    internal EFTCameraHook()
    {
        CameraManager.Instance.OnCameraChanged += OnCameraChanged;

        if (CameraManager.Instance.Camera != null)
        {
            OnCameraChanged();
        }
    }

    void OnCameraChanged()
    {
        CameraManager.Instance.Camera.gameObject.GetOrAddComponent<EFTScreenGrabber>();
    }

    public void Dispose()
    {
        CameraManager.Instance.OnCameraChanged -= OnCameraChanged;

        if (CameraManager.Instance.Camera != null)
        {
            if (CameraManager.Instance.Camera.TryGetComponent<EFTScreenGrabber>(out var component))
            {
                Component.Destroy(component);
            }
        }
    }
}


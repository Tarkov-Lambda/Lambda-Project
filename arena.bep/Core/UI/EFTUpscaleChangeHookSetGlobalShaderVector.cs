using System;
using UnityEngine;

namespace ifp.arena.bep.Core.UI
{
    internal class EFTUpscaleChangeHookSetGlobalShaderVector : IDisposable
    {
        readonly int propertyHash;

        internal EFTUpscaleChangeHookSetGlobalShaderVector(string globalPropertyName)
        {
            propertyHash = Shader.PropertyToID(globalPropertyName);

            CameraManager.Instance.OnCameraChanged += OnCameraChanged;

            if (CameraManager.Instance.Camera != null)
            {
                UpdateGlobalGrabPassScale();
            }
        }

        private void OnCameraChanged()
        {
            CameraManager.Instance.Camera.GetComponent<SSAAImpl>().RenderTexturesAreChanged += UpdateGlobalGrabPassScale;
            UpdateGlobalGrabPassScale();
        }

        private void UpdateGlobalGrabPassScale()
        {
            SSAA ssaa = CameraManager.Instance.SSAA;

            Vector2 globalGrabScale = Vector2.one;

            if (ssaa != null)
            {
                float inputW = ssaa.GetInputWidth();
                float inputH = ssaa.GetInputHeight();
                float outputW = ssaa.GetOutputWidth();
                float outputH = ssaa.GetOutputHeight();

                if (outputW > 0 && outputH > 0)
                {
                    globalGrabScale.x = outputW / inputW;
                    globalGrabScale.y = outputH/ inputH ;
                }
            }

            Shader.SetGlobalVector(propertyHash, globalGrabScale);
        }

        public void Dispose()
        {
            CameraManager.Instance.OnCameraChanged -= OnCameraChanged;

            if (CameraManager.Instance.Camera != null)
            {
                if (CameraManager.Instance.Ssaaimpl_0 != null)
                    CameraManager.Instance.Ssaaimpl_0.RenderTexturesAreChanged -= UpdateGlobalGrabPassScale;
            }
        }
    }
}

using UnityEngine;
using UnityEngine.Rendering;

namespace ifp.arena.bep.Core.UI
{
    internal class EFTScreenGrabber : MonoBehaviour
    {
        private Camera _camera;
        private CommandBuffer _commandBuffer;
        private RenderTexture _screenCapture;

        void OnEnable()
        {
            _camera = GetComponent<Camera>();
            if (_camera == null)
            {
                this.enabled = false;
                return;
            }

            _commandBuffer = new CommandBuffer { name = "UI_GrabScreen" };

            _camera.AddCommandBuffer(CameraEvent.AfterEverything, _commandBuffer);

            RebuildResources();
        }

        void RebuildResources()
        {
            if (_commandBuffer == null) return;

            _commandBuffer.Clear();

            if (_screenCapture != null)
            {
                _screenCapture.Release();
                _screenCapture = null;
            }

            _screenCapture = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBHalf);
            _screenCapture.name = "GlobalUI_ScreenGrab";
            Shader.SetGlobalTexture("_GlobalScreenGrab", _screenCapture);

            _commandBuffer.Blit(BuiltinRenderTextureType.CameraTarget, _screenCapture);
        }

        void OnDisable()
        {
            if (_camera != null && _commandBuffer != null)
            {
                _camera.RemoveCommandBuffer(CameraEvent.AfterEverything, _commandBuffer);
            }

            if (_commandBuffer != null) _commandBuffer.Release();
            if (_screenCapture != null) _screenCapture.Release();
        }
    }
}

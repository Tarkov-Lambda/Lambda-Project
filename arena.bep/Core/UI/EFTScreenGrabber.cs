using UnityEngine;
using UnityEngine.Rendering;

namespace ifp.arena.bep.Core.UI
{
    internal class EFTScreenGrabber : MonoBehaviour
    {
        private Camera _camera;
        private SSAA _ssaa;

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

            _ssaa = _camera.GetComponent<SSAA>();

            _commandBuffer = new CommandBuffer { name = "UI_GrabScreen" };

            _camera.AddCommandBuffer(CameraEvent.AfterImageEffects, _commandBuffer);

            if (_ssaa != null)
                _ssaa.GetComponent<SSAAImpl>().RenderTexturesAreChanged += RebuildResources;

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

            int w = Screen.width;
            int h = Screen.height;

            _screenCapture = new RenderTexture(w, h, 0, RenderTextureFormat.ARGBHalf);
            _screenCapture.name = "GlobalUI_ScreenGrab";
            Shader.SetGlobalTexture("_GlobalScreenGrab", _screenCapture);

            if (_ssaa != null)
            {
                RenderTexture src, dst;
                var prop = _ssaa.GetComponent<SSAAPropagator>();
                if (prop != null && prop.GetSourceDestination(out src, out dst) && src != null)
                {
                    _commandBuffer.Blit(src, _screenCapture, new Vector2(1, -1f), new Vector2(0, 1));
                    return;
                }
            }

            _commandBuffer.Blit(BuiltinRenderTextureType.CameraTarget, _screenCapture);
        }

        void OnDisable()
        {
            if (_camera != null && _commandBuffer != null)
                _camera.RemoveCommandBuffer(CameraEvent.AfterImageEffects, _commandBuffer);

            if (_ssaa != null)
            {
                var impl = _ssaa.GetComponent<SSAAImpl>();
                if (impl != null) impl.RenderTexturesAreChanged -= RebuildResources;
            }

            if (_commandBuffer != null) _commandBuffer.Release();
            if (_screenCapture != null) _screenCapture.Release();
        }
    }
}

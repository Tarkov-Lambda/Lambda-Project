using UnityEngine;
using UnityEngine.Rendering;
using System.Collections;

namespace Lambda.Core.Main.UI;

internal class EFTScreenGrabber : MonoBehaviour
{
    private Camera _camera;
    private SSAA _ssaa;

    private CommandBuffer _commandBuffer;
    private RenderTexture _screenCapture;

    void Start()
    {
        _camera = GetComponent<Camera>();
        if (_camera == null)
        {
            Destroy(this);
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
        StartCoroutine(RebuildResourcesCoroutine());
    }

    IEnumerator RebuildResourcesCoroutine()
    {
        if (_commandBuffer == null) yield break;

        _commandBuffer.Clear();

        if (_screenCapture != null)
        {
            _screenCapture.Release();
            _screenCapture = null;
        }

        yield return null; // camera's SSAA needs 1 frame to properly initialize

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
                yield break;
            }
        }

        _commandBuffer.Blit(BuiltinRenderTextureType.CameraTarget, _screenCapture);
    }

    void OnDestroy()
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

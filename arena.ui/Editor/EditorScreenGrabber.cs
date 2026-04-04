#if UNITY_EDITOR
using UnityEngine;

namespace ifp.arena.bep.Core.UI.Editor
{
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    public class EditorScreenGrabber : MonoBehaviour
    {
        private RenderTexture _screenCapture;

        void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (_screenCapture == null || _screenCapture.width != source.width || _screenCapture.height != source.height)
            {
                if (_screenCapture != null)
                {
                    _screenCapture.Release();
                }

                _screenCapture = new RenderTexture(source.width, source.height, 0, RenderTextureFormat.ARGBHalf);
                _screenCapture.name = "EditorUI_ScreenGrab";

                Shader.SetGlobalTexture("_GlobalScreenGrab", _screenCapture);
            }

            Graphics.Blit(source, _screenCapture);

            Graphics.Blit(source, destination);
        }

        void OnDisable()
        {
            if (_screenCapture != null)
            {
                _screenCapture.Release();
                _screenCapture = null;
            }
        }
    }
}
#endif
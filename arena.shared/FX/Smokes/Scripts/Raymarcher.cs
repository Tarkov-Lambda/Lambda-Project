using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(Camera))]
public class Raymarcher : MonoBehaviour
{
    // ── External references ──────────────────────────────────────────────────
    public Voxelizer smokeVoxelData = null;
    public Gun gun = null;

    /// <summary>Directional light whose forward is used as _SunDirection.
    /// Set this from RaymarchHandler before enabling the component.</summary>
    public Transform sunTransform;

    // ── Resolution ───────────────────────────────────────────────────────────
    public enum Res { FullResolution = 0, HalfResolution, QuarterResolution }
    public Res resolutionScale;

    // ── Noise Settings ───────────────────────────────────────────────────────
    [Header("Noise Settings"), Space(5)]
    [Range(0, 100000)] public int seed = 0;
    [Range(1, 16)]     public int octaves = 1;
    [Range(1, 128)]    public int cellSize = 16;
    [Range(1, 64)]     public int axisCellCount = 4;
    [Range(0.1f, 16f)] public float amplitude = 1.0f;
    [Range(0f, 5f)]    public float warp = 0.0f;
    [Range(-5f, 5f)]   public float add = 0.0f;
    public bool invertNoise = false;
    public bool updateNoise = false;

    // Debug noise (editor only)
    public bool debugNoise = false;
    public bool debugTiledNoise = false;
    public enum DebugAxis { X = 0, Y, Z }
    public DebugAxis debugNoiseAxis;
    [Range(0, 128)] public int debugNoiseSlice = 0;

    // ── SDF Settings ─────────────────────────────────────────────────────────
    [Header("SDF Settings"), Space(5)]
    public Vector4 cubeParams = new Vector4(0, 0, 0, 1);

    // ── Smoke Settings ───────────────────────────────────────────────────────
    [Header("Smoke Settings"), Space(5)]
    [ColorUsage(false, true)] public Color lightColor;
    public Color smokeColor;
    [Range(1, 256)]    public int   stepCount             = 64;
    [Range(0.01f, 0.1f)] public float stepSize            = 0.05f;
    [Range(1, 32)]     public int   lightStepCount        = 8;
    [Range(0.01f, 1f)] public float lightStepSize         = 0.25f;
    [Range(0.01f, 64f)] public float smokeSize            = 32.0f;
    [Range(0f, 10f)]   public float volumeDensity         = 1.0f;
    [Range(0f, 3f)]    public float absorptionCoefficient = 0.5f;
    [Range(0f, 3f)]    public float scatteringCoefficient = 0.5f;
    public Color extinctionColor = new Color(1, 1, 1);
    [Range(0f, 10f)]   public float shadowDensity         = 1.0f;

    public enum PhaseFunction { HenyeyGreenstein = 0, Mie, Rayleigh }
    public PhaseFunction phaseFunction;

    [Range(-1f, 1f)] public float scatteringAnisotropy = 0.0f;
    [Range(0f, 1f)]  public float densityFalloff       = 0.25f;
    [Range(0f, 1f)]  public float alphaThreshold       = 0.1f;

    // ── Animation Settings ───────────────────────────────────────────────────
    [Header("Animation Settings"), Space(5)]
    public Vector3 animationDirection = new Vector3(0, -0.1f, 0);

    // ── Composite Settings ───────────────────────────────────────────────────
    [Header("Composite Settings"), Space(5)]
    public bool bicubicUpscale = true;
    [Range(-1f, 1f)] public float sharpness = 0.0f;

    public enum ViewTexture { Composite = 0, SmokeAlbedo, SmokeMask, PolygonalDepth }
    public ViewTexture debugView;

    // ── Assets (injected by RaymarchHandler at runtime) ──────────────────────
    public Material       compositeMaterial;
    public ComputeShader  raymarchCompute;

    // ── Private state ────────────────────────────────────────────────────────
    private Camera _cam;
    private CommandBuffer _cmd;

    // Kernel handles
    private int _kGenerateNoise, _kDebugNoise, _kRaymarch;

    // Render textures
    private RenderTexture _noiseTex;
    private RenderTexture _depthTex;
    private RenderTexture _albedoFull,    _albedoHalf,    _albedoQuarter;
    private RenderTexture _maskFull,      _maskHalf,      _maskQuarter;

    // Cached resolution for resize detection
    private int _allocWidth, _allocHeight;

    // Temp RT name, allocated inside the command buffer each frame
    private static readonly int _tempColorID = Shader.PropertyToID("_SmokeSceneColorTex");

    // ════════════════════════════════════════════════════════════════════════
    // MonoBehaviour lifecycle
    // ════════════════════════════════════════════════════════════════════════

    void OnEnable()
    {
#if UNITY_EDITOR
        compositeMaterial = new Material(Shader.Find("Hidden/CompositeEffects"));
        raymarchCompute = (ComputeShader)Resources.Load("RenderSmoke");
#endif
        if (raymarchCompute == null || compositeMaterial == null)
        {
            Debug.LogWarning("[Raymarcher] raymarchCompute or compositeMaterial not set — disabling.");
            enabled = false;
            return;
        }

        _cam = GetComponent<Camera>();
        // Ensure Unity generates the depth texture so _CameraDepthTexture is valid.
        _cam.depthTextureMode |= DepthTextureMode.Depth;

        _kGenerateNoise = raymarchCompute.FindKernel("CS_GenerateNoise");
        _kDebugNoise    = raymarchCompute.FindKernel("CS_DebugNoise");
        _kRaymarch      = raymarchCompute.FindKernel("CS_RayMarchSmoke");

        AllocateRTs(_cam.pixelWidth, _cam.pixelHeight);
        InitializeNoise();

        _cmd = new CommandBuffer { name = "SmokeRaymarch" };
        _cam.AddCommandBuffer(CameraEvent.BeforeImageEffects, _cmd);
    }

    void OnDisable()
    {
        if (_cam != null && _cmd != null)
            _cam.RemoveCommandBuffer(CameraEvent.BeforeImageEffects, _cmd);

        _cmd?.Dispose();
        _cmd = null;

        ReleaseRTs();
    }

    void LateUpdate()
    {
        if (_cam == null || raymarchCompute == null || compositeMaterial == null)
            return;

        // ── Resize detection ────────────────────────────────────────────────
        int w = _cam.pixelWidth;
        int h = _cam.pixelHeight;
        if (w != _allocWidth || h != _allocHeight)
        {
            _cam.RemoveCommandBuffer(CameraEvent.BeforeImageEffects, _cmd);
            _cmd?.Dispose();
            ReleaseRTs();
            AllocateRTs(w, h);
            _cmd = new CommandBuffer { name = "SmokeRaymarch" };
            _cam.AddCommandBuffer(CameraEvent.BeforeImageEffects, _cmd);
        }

        // ── Noise update (direct dispatch, not via cmd buffer) ───────────────
        if (updateNoise)
            UpdateNoise();

        // ── Per-frame compute buffer bindings ────────────────────────────────
        // These are set on the compute shader object directly because they are
        // struct buffer handles that don't change format, only content.
        if (smokeVoxelData != null)
        {
            ComputeBuffer voxelBuf = smokeVoxelData.GetSmokeVoxelBuffer();
            if (voxelBuf != null)
            {
                raymarchCompute.SetBuffer(_kRaymarch, "_SmokeVoxels", voxelBuf);
                raymarchCompute.SetVector("_BoundsExtent",   smokeVoxelData.GetBoundsExtent());
                raymarchCompute.SetVector("_VoxelResolution", smokeVoxelData.GetVoxelResolution());
            }
        }

        if (gun != null)
        {
            ComputeBuffer holeBuf = gun.GetBulletHoles();
            if (holeBuf != null)
            {
                raymarchCompute.SetBuffer(_kRaymarch, "_BulletHoles", holeBuf);
                raymarchCompute.SetFloat("_BulletDepth", gun.GetDepth());
                raymarchCompute.SetInt("_BulletHoleCount", gun.GetActiveBulletHoleCount());
            }
        }

        // ── (Re)build command buffer for this frame ──────────────────────────
        RebuildCommandBuffer();
    }

    // ════════════════════════════════════════════════════════════════════════
    // RT allocation / release
    // ════════════════════════════════════════════════════════════════════════

    private void AllocateRTs(int width, int height)
    {
        _allocWidth  = width;
        _allocHeight = height;

        int hw = Mathf.CeilToInt(width  * 0.5f);
        int hh = Mathf.CeilToInt(height * 0.5f);
        int qw = Mathf.CeilToInt(width  * 0.25f);
        int qh = Mathf.CeilToInt(height * 0.25f);

        _albedoFull    = MakeRT(width,  height, RenderTextureFormat.ARGB64);
        _albedoHalf    = MakeRT(hw,     hh,     RenderTextureFormat.ARGB64);
        _albedoQuarter = MakeRT(qw,     qh,     RenderTextureFormat.ARGB64);

        _maskFull    = MakeRT(width, height, RenderTextureFormat.RFloat);
        _maskHalf    = MakeRT(hw,    hh,     RenderTextureFormat.RFloat);
        _maskQuarter = MakeRT(qw,    qh,     RenderTextureFormat.RFloat);

        _depthTex = MakeRT(width, height, RenderTextureFormat.RHalf);
    }

    private static RenderTexture MakeRT(int w, int h, RenderTextureFormat fmt)
    {
        var rt = new RenderTexture(w, h, 0, fmt, RenderTextureReadWrite.Linear)
        {
            enableRandomWrite = true
        };
        rt.Create();
        return rt;
    }

    private void ReleaseRTs()
    {
        SafeRelease(ref _albedoFull);
        SafeRelease(ref _albedoHalf);
        SafeRelease(ref _albedoQuarter);
        SafeRelease(ref _maskFull);
        SafeRelease(ref _maskHalf);
        SafeRelease(ref _maskQuarter);
        SafeRelease(ref _depthTex);
        SafeRelease(ref _noiseTex);
    }

    private static void SafeRelease(ref RenderTexture rt)
    {
        if (rt == null) return;
        rt.Release();
        DestroyImmediate(rt);
        rt = null;
    }

    // ════════════════════════════════════════════════════════════════════════
    // Noise (direct dispatch — one-shot or on-demand, never in the cmd buffer)
    // ════════════════════════════════════════════════════════════════════════

    private void InitializeNoise()
    {
        if (_noiseTex != null)
        {
            UpdateNoise();
            return;
        }

        _noiseTex = new RenderTexture(128, 128, 0, RenderTextureFormat.RHalf, RenderTextureReadWrite.Linear)
        {
            enableRandomWrite = true,
            dimension         = UnityEngine.Rendering.TextureDimension.Tex3D,
            volumeDepth       = 128
        };
        _noiseTex.Create();
        UpdateNoise();
    }

    private void UpdateNoise()
    {
        raymarchCompute.SetTexture(_kGenerateNoise, "_RWNoiseTex", _noiseTex);
        raymarchCompute.SetInt("_Octaves",     octaves);
        raymarchCompute.SetInt("_CellSize",    cellSize);
        raymarchCompute.SetInt("_AxisCellCount", axisCellCount);
        raymarchCompute.SetFloat("_Amplitude", amplitude);
        raymarchCompute.SetFloat("_Warp",      warp);
        raymarchCompute.SetFloat("_Add",       add);
        raymarchCompute.SetInt("_InvertNoise", invertNoise ? 1 : 0);
        raymarchCompute.SetInt("_Seed",        seed);
        raymarchCompute.SetVector("_NoiseRes", new Vector4(128, 128, 128, 0));
        // 128 / 8 = 16 thread groups per axis
        raymarchCompute.Dispatch(_kGenerateNoise, 16, 16, 16);
    }

    // ════════════════════════════════════════════════════════════════════════
    // Command buffer construction — called every LateUpdate
    // ════════════════════════════════════════════════════════════════════════

    private void RebuildCommandBuffer()
    {
        _cmd.Clear();

        RenderTexture albedoTex = ActiveAlbedoTex();
        RenderTexture maskTex   = ActiveMaskTex();

        // ── 1. Extract depth from Unity's depth texture into our own RT ──────
        // Pass 0 of CompositeEffects reads _CameraDepthTexture (global) and
        // ignores _MainTex, so the source is a dummy.
        _cmd.Blit(BuiltinRenderTextureType.CurrentActive, _depthTex, compositeMaterial, 0);

        // ── 2. Snapshot the scene colour buffer ──────────────────────────────
        // We can't read and write CameraTarget simultaneously, so we copy it.
        _cmd.GetTemporaryRT(_tempColorID, _allocWidth, _allocHeight, 0,
            FilterMode.Point,
            _cam.allowHDR ? RenderTextureFormat.ARGBHalf : RenderTextureFormat.ARGB32);
        _cmd.Blit(BuiltinRenderTextureType.CameraTarget, _tempColorID);

        // ── 3. Build per-frame camera matrices ───────────────────────────────
        Matrix4x4 proj     = GL.GetGPUProjectionMatrix(_cam.projectionMatrix, false);
        Matrix4x4 viewProj = proj * _cam.worldToCameraMatrix;

        // ── 4. Record all compute shader parameters ──────────────────────────
        int k = _kRaymarch;

        // Textures
        _cmd.SetComputeTextureParam(raymarchCompute, k, "_SmokeTex",     albedoTex);
        _cmd.SetComputeTextureParam(raymarchCompute, k, "_SmokeMaskTex", maskTex);
        _cmd.SetComputeTextureParam(raymarchCompute, k, "_NoiseTex",     _noiseTex);
        _cmd.SetComputeTextureParam(raymarchCompute, k, "_DepthTex",     _depthTex);

        // Camera
        _cmd.SetComputeVectorParam(raymarchCompute, "_CameraForward",            _cam.transform.forward);
        _cmd.SetComputeVectorParam(raymarchCompute, "_CameraWorldPos",           _cam.transform.position);
        _cmd.SetComputeMatrixParam(raymarchCompute, "_CameraToWorld",            _cam.cameraToWorldMatrix);
        _cmd.SetComputeMatrixParam(raymarchCompute, "_CameraInvProjection",      proj.inverse);
        _cmd.SetComputeMatrixParam(raymarchCompute, "_CameraInvViewProjection",  viewProj.inverse);
        _cmd.SetComputeIntParam(   raymarchCompute, "_BufferWidth",              albedoTex.width);
        _cmd.SetComputeIntParam(   raymarchCompute, "_BufferHeight",             albedoTex.height);

        // Lighting / phase
        Vector4 sunDir = sunTransform != null
            ? new Vector4(sunTransform.forward.x, sunTransform.forward.y, sunTransform.forward.z, 0f)
            : Vector4.zero;
        _cmd.SetComputeVectorParam(raymarchCompute, "_SunDirection",  sunDir);
        _cmd.SetComputeVectorParam(raymarchCompute, "_LightColor",    lightColor);
        _cmd.SetComputeIntParam(   raymarchCompute, "_PhaseFunction", (int)phaseFunction);
        _cmd.SetComputeFloatParam( raymarchCompute, "_G",             scatteringAnisotropy);

        // Smoke volume
        _cmd.SetComputeVectorParam(raymarchCompute, "_SmokeColor",             smokeColor);
        _cmd.SetComputeVectorParam(raymarchCompute, "_ExtinctionColor",        extinctionColor);
        _cmd.SetComputeVectorParam(raymarchCompute, "_AnimationDirection",     animationDirection);
        _cmd.SetComputeVectorParam(raymarchCompute, "_CubeParams",             cubeParams);
        _cmd.SetComputeIntParam(   raymarchCompute, "_StepCount",              stepCount);
        _cmd.SetComputeIntParam(   raymarchCompute, "_LightStepCount",         lightStepCount);
        _cmd.SetComputeFloatParam( raymarchCompute, "_StepSize",               stepSize);
        _cmd.SetComputeFloatParam( raymarchCompute, "_LightStepSize",          lightStepSize);
        _cmd.SetComputeFloatParam( raymarchCompute, "_SmokeSize",              smokeSize);
        _cmd.SetComputeFloatParam( raymarchCompute, "_FrameTime",              Time.time);
        _cmd.SetComputeFloatParam( raymarchCompute, "_VolumeDensity",          volumeDensity * stepSize);
        _cmd.SetComputeFloatParam( raymarchCompute, "_ShadowDensity",          shadowDensity * lightStepSize);
        _cmd.SetComputeFloatParam( raymarchCompute, "_AbsorptionCoefficient",  absorptionCoefficient);
        _cmd.SetComputeFloatParam( raymarchCompute, "_ScatteringCoefficient",  scatteringCoefficient);
        _cmd.SetComputeFloatParam( raymarchCompute, "_DensityFalloff",         1f - densityFalloff);
        _cmd.SetComputeFloatParam( raymarchCompute, "_AlphaThreshold",         alphaThreshold);

        // Smoke voxel / spatial
        if (smokeVoxelData != null)
        {
            Vector3 radius = smokeVoxelData.GetSmokeRadius();
            Vector3 origin = smokeVoxelData.GetSmokeOrigin();
            _cmd.SetComputeVectorParam(raymarchCompute, "_Radius",
                new Vector4(radius.x, radius.y, radius.z, 0f));
            _cmd.SetComputeVectorParam(raymarchCompute, "_SmokeOrigin",
                new Vector4(origin.x, origin.y, origin.z, 0f));
        }

        // ── 5. Dispatch the raymarcher ────────────────────────────────────────
        _cmd.DispatchCompute(raymarchCompute, k,
            Mathf.CeilToInt(albedoTex.width  / 8f),
            Mathf.CeilToInt(albedoTex.height / 8f),
            1);

        // ── 6. Upscale to full resolution (if running at reduced res) ─────────
        switch (resolutionScale)
        {
            case Res.HalfResolution:
                // Ping-pong the mask so the full-res mask is up-to-date
                _cmd.Blit(_maskHalf, _maskFull);
                _cmd.Blit(_maskFull, _maskHalf);

                if (bicubicUpscale)
                    _cmd.Blit(_albedoHalf, _albedoFull, compositeMaterial, 1);
                else
                    _cmd.Blit(_albedoHalf, _albedoFull);
                break;

            case Res.QuarterResolution:
                _cmd.Blit(_maskQuarter, _maskHalf);
                _cmd.Blit(_maskHalf,    _maskFull);
                _cmd.Blit(_maskFull,    _maskHalf);
                _cmd.Blit(_maskHalf,    _maskQuarter);

                if (bicubicUpscale)
                {
                    _cmd.Blit(_albedoQuarter, _albedoHalf, compositeMaterial, 1);
                    _cmd.Blit(_albedoHalf,    _albedoFull, compositeMaterial, 1);
                }
                else
                {
                    _cmd.Blit(_albedoQuarter, _albedoHalf);
                    _cmd.Blit(_albedoHalf,    _albedoFull);
                }
                break;
        }

        // ── 7. Composite smoke onto scene colour → write back to CameraTarget ─
        // Pass 2 reads _MainTex (= scene snapshot) + globals below.
        _cmd.SetGlobalTexture("_SmokeTex",     _albedoFull);
        _cmd.SetGlobalTexture("_SmokeMaskTex", _maskFull);
        _cmd.SetGlobalTexture("_DepthTex",     _depthTex);
        _cmd.SetGlobalFloat(  "_Sharpness",    sharpness);
        _cmd.SetGlobalInt(    "_DebugView",    (int)debugView);
        _cmd.Blit(_tempColorID, BuiltinRenderTextureType.CameraTarget, compositeMaterial, 2);

        _cmd.ReleaseTemporaryRT(_tempColorID);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private RenderTexture ActiveAlbedoTex() => resolutionScale switch
    {
        Res.HalfResolution    => _albedoHalf,
        Res.QuarterResolution => _albedoQuarter,
        _                     => _albedoFull,
    };

    private RenderTexture ActiveMaskTex() => resolutionScale switch
    {
        Res.HalfResolution    => _maskHalf,
        Res.QuarterResolution => _maskQuarter,
        _                     => _maskFull,
    };
}

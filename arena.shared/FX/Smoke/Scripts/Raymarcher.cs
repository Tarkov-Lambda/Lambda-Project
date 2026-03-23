using UnityEngine;
using UnityEngine.Rendering;

public class Raymarcher : MonoBehaviour {

    // ── References ────────────────────────────────────────────────────────────

    public Voxelizer smokeVoxelData = null;
    public Gun       gun            = null;

    public Camera     cam;

    public Material      compositeMaterial;
    public ComputeShader raymarchCompute;
    // ── Resolution ────────────────────────────────────────────────────────────

    public enum Res { FullResolution = 0, HalfResolution, QuarterResolution }
    public Res resolutionScale;

    // ── Noise ─────────────────────────────────────────────────────────────────

    [Header("Noise Settings")]
    [Range(0, 100000)]   public int   seed         = 0;
    [Range(1, 16)]       public int   octaves      = 1;
    [Range(1, 128)]      public int   cellSize     = 16;
    [Range(1, 64)]       public int   axisCellCount = 4;
    [Range(0.1f, 16f)]   public float amplitude    = 1.0f;
    [Range(0.0f, 5.0f)]  public float warp         = 0.0f;
    [Range(-5.0f, 5.0f)] public float add          = 0.0f;
    public bool invertNoise  = false;
    public bool updateNoise  = false;
    public bool debugNoise   = false;
    public bool debugTiledNoise = false;

    public enum DebugAxis { X = 0, Y, Z }
    public DebugAxis debugNoiseAxis;
    [Range(0, 128)] public int debugNoiseSlice = 0;

    // ── SDF ───────────────────────────────────────────────────────────────────

    [Header("SDF Settings")]
    public Vector4 cubeParams = new Vector4(0, 0, 0, 1);

    // ── Smoke ─────────────────────────────────────────────────────────────────

    [Header("Smoke Settings")]
    [ColorUsageAttribute(false, true)] public Color lightColor;
    public Color smokeColor;

    [Range(1, 256)]      public int   stepCount              = 64;
    [Range(0.01f, 0.1f)] public float stepSize               = 0.05f;
    [Range(1, 32)]       public int   lightStepCount         = 8;
    [Range(0.01f, 1.0f)] public float lightStepSize          = 0.25f;
    [Range(0.01f, 64f)]  public float smokeSize              = 32.0f;
    [Range(0.0f, 10f)]   public float volumeDensity          = 1.0f;
    [Range(0.0f, 3.0f)]  public float absorptionCoefficient  = 0.5f;
    [Range(0.0f, 3.0f)]  public float scatteringCoefficient  = 0.5f;
    public Color extinctionColor = new Color(1, 1, 1);
    [Range(0.0f, 10f)]   public float shadowDensity          = 1.0f;

    public enum PhaseFunction { HenyeyGreenstein = 0, Mie, Rayleigh }
    public PhaseFunction phaseFunction;

    [Range(-1.0f, 1.0f)] public float scatteringAnisotropy = 0.0f;
    [Range(0.0f, 1.0f)]  public float densityFalloff       = 0.25f;
    [Range(0.0f, 1.0f)]  public float alphaThreshold       = 0.1f;

    // ── Animation ─────────────────────────────────────────────────────────────

    [Header("Animation Settings")]
    public Vector3 animationDirection = new Vector3(0, -0.1f, 0);

    // ── Composite ─────────────────────────────────────────────────────────────

    [Header("Composite Settings")]
    public bool  bicubicUpscale = true;
    [Range(-1.0f, 1.0f)] public float sharpness = 0.0f;

    public enum ViewTexture { Composite = 0, SmokeAlbedo, SmokeMask, PolygonalDepth }
    public ViewTexture debugView;

    // ── Private state ─────────────────────────────────────────────────────────

    private GameObject sun;


    // Kernel handles
    private int generateNoisePass;
    private int debugNoisePass;
    private int raymarchSmokePass;
    private int bulletHoleBuildPass;   // CS_BuildBulletHoleMask (kernel 3)

    // Screen-space textures
    private RenderTexture noiseTex, depthTex;
    private RenderTexture smokeAlbedoFullTex,    smokeAlbedoHalfTex,    smokeAlbedoQuarterTex;
    private RenderTexture smokeMaskFullTex,       smokeMaskHalfTex,       smokeMaskQuarterTex;

    // Bullet hole bitmask — 3D RFloat, same dims as smoke voxel grid
    // Written each frame by CS_BuildBulletHoleMask; sampled in CS_RayMarchSmoke
    private RenderTexture bulletHoleMaskTex;
    private bool          wasBuildingBulletHoles = false;

    // Fallback 1-element buffer kept permanently bound to CS_BuildBulletHoleMask._BulletHoles.
    // Unity requires every declared buffer to be bound at dispatch time even when
    // _BulletHoleCount == 0 and the loop body is never entered.
    // GPUBulletHole = float3 origin + float3 forward + float2 radius = 8 floats = 32 bytes.
    private ComputeBuffer dummyBulletHoleBuffer;


    // ── Noise ─────────────────────────────────────────────────────────────────

    void UpdateNoise() {
        raymarchCompute.SetTexture(generateNoisePass, "_RWNoiseTex", noiseTex);
        raymarchCompute.SetInt("_Octaves",       octaves);
        raymarchCompute.SetInt("_CellSize",      cellSize);
        raymarchCompute.SetInt("_AxisCellCount", axisCellCount);
        raymarchCompute.SetFloat("_Amplitude",   amplitude);
        raymarchCompute.SetFloat("_Warp",        warp);
        raymarchCompute.SetFloat("_Add",         add);
        raymarchCompute.SetInt("_InvertNoise",   invertNoise ? 1 : 0);
        raymarchCompute.SetInt("_Seed",          seed);
        raymarchCompute.SetVector("_NoiseRes",   new Vector4(128, 128, 128, 0));
        raymarchCompute.Dispatch(generateNoisePass, 16, 16, 16); // 128/8
        raymarchCompute.SetTexture(raymarchSmokePass, "_NoiseTex", noiseTex);
    }

    void InitializeNoise() {
        if (noiseTex != null) { UpdateNoise(); return; }

        noiseTex = new RenderTexture(128, 128, 0, RenderTextureFormat.RHalf, RenderTextureReadWrite.Linear) {
            enableRandomWrite = true,
            dimension         = TextureDimension.Tex3D,
            volumeDepth       = 128
        };
        noiseTex.Create();
        UpdateNoise();
    }


    // ── Initialization ────────────────────────────────────────────────────────

    void InitializeVariables() {

        generateNoisePass    = raymarchCompute.FindKernel("CS_GenerateNoise");
        debugNoisePass       = raymarchCompute.FindKernel("CS_DebugNoise");
        raymarchSmokePass    = raymarchCompute.FindKernel("CS_RayMarchSmoke");
        bulletHoleBuildPass  = raymarchCompute.FindKernel("CS_BuildBulletHoleMask");

        InitializeNoise();

        int w  = Screen.width,  h  = Screen.height;
        int hw = Mathf.CeilToInt(w / 2f), hh = Mathf.CeilToInt(h / 2f);
        int qw = Mathf.CeilToInt(w / 4f), qh = Mathf.CeilToInt(h / 4f);

        smokeAlbedoFullTex    = CreateRT(w,  h,  RenderTextureFormat.ARGB64);
        smokeAlbedoHalfTex    = CreateRT(hw, hh, RenderTextureFormat.ARGB64);
        smokeAlbedoQuarterTex = CreateRT(qw, qh, RenderTextureFormat.ARGB64);

        smokeMaskFullTex    = CreateRT(w,  h,  RenderTextureFormat.RFloat);
        smokeMaskHalfTex    = CreateRT(hw, hh, RenderTextureFormat.RFloat);
        smokeMaskQuarterTex = CreateRT(qw, qh, RenderTextureFormat.RFloat);

        depthTex = CreateRT(w, h, RenderTextureFormat.RHalf);

        // Permanent dummy binding so CS_BuildBulletHoleMask._BulletHoles is always satisfied.
        // stride = float3 + float3 + float2 = 8 floats = 32 bytes (matches Gun's GPUBulletHole).
        dummyBulletHoleBuffer = new ComputeBuffer(1, sizeof(float) * 8);
        raymarchCompute.SetBuffer(bulletHoleBuildPass, "_BulletHoles", dummyBulletHoleBuffer);
    }

    static RenderTexture CreateRT(int w, int h, RenderTextureFormat fmt) {
        var rt = new RenderTexture(w, h, 0, fmt, RenderTextureReadWrite.Linear);
        rt.enableRandomWrite = true;
        rt.Create();
        return rt;
    }

    // Lazily creates the bullet hole mask texture once smokeVoxelData is available.
    // Immediately initializes it to all 1.0f (no attenuation) via a zero-hole dispatch.
    void InitBulletHoleMask() {
        Vector3 voxelRes = smokeVoxelData.GetVoxelResolution();
        int rx = (int)voxelRes.x, ry = (int)voxelRes.y, rz = (int)voxelRes.z;

        bulletHoleMaskTex = new RenderTexture(rx, ry, 0, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear) {
            enableRandomWrite = true,
            dimension         = TextureDimension.Tex3D,
            volumeDepth       = rz,
            filterMode        = FilterMode.Bilinear,
            wrapMode          = TextureWrapMode.Clamp
        };
        bulletHoleMaskTex.Create();

        // Prime all texels to 1.0f — dispatching with BulletHoleCount=0 writes minDist=1 everywhere
        SetSmokeVolumeUniforms();
        raymarchCompute.SetInt("_BulletHoleCount", 0);
        raymarchCompute.SetTexture(bulletHoleBuildPass, "_BulletHoleMaskTexRW", bulletHoleMaskTex);
        DispatchFullSmoke(bulletHoleBuildPass);
    }

    // Dispatches a kernel over the smoke voxel grid (4,4,8 thread groups)
    void DispatchFullSmoke(int kernel) {
        Vector3 voxelRes = smokeVoxelData.GetVoxelResolution();
        raymarchCompute.Dispatch(kernel,
            Mathf.CeilToInt(voxelRes.x / 4.0f),
            Mathf.CeilToInt(voxelRes.y / 4.0f),
            Mathf.CeilToInt(voxelRes.z / 8.0f));
    }

    // Sets uniforms shared between CS_BuildBulletHoleMask and CS_RayMarchSmoke
    void SetSmokeVolumeUniforms() {
        raymarchCompute.SetVector("_BoundsExtent",    smokeVoxelData.GetBoundsExtent());
        raymarchCompute.SetVector("_VoxelResolution", smokeVoxelData.GetVoxelResolution());
        raymarchCompute.SetFloat("_VoxelSize",        smokeVoxelData.GetVoxelSize());
    }


    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void OnEnable() {
        cam = GetComponentInParent<Camera>();

        InitializeVariables();

#if UNITY_EDITOR
        smokeVoxelData.HandleSmokeThrow(cam.transform.position);
#endif

    }

    void OnDisable() {
        bulletHoleMaskTex?.Release();
        bulletHoleMaskTex = null;
        dummyBulletHoleBuffer?.Release();
        dummyBulletHoleBuffer = null;
    }

    void Update() {
        if (updateNoise) UpdateNoise();

        if (smokeVoxelData == null) return;

        // Bind smoke voxel texture (hardware-trilinear, replaces old _SmokeVoxels buffer)
        SetSmokeVolumeUniforms();
        raymarchCompute.SetTexture(raymarchSmokePass, "_SmokeVoxelTex", smokeVoxelData.GetSmokeVoxelTexture());
    }


    // ── Render loop ───────────────────────────────────────────────────────────

    private RenderTexture GetSmokeAlbedoTex() {
        return resolutionScale == Res.QuarterResolution ? smokeAlbedoQuarterTex
             : resolutionScale == Res.HalfResolution    ? smokeAlbedoHalfTex
             : smokeAlbedoFullTex;
    }

    private RenderTexture GetSmokeMaskTex() {
        return resolutionScale == Res.QuarterResolution ? smokeMaskQuarterTex
             : resolutionScale == Res.HalfResolution    ? smokeMaskHalfTex
             : smokeMaskFullTex;
    }

    void OnRenderImage(RenderTexture source, RenderTexture destination) {
        if (smokeVoxelData == null) { Graphics.Blit(source, destination); return; }

        RenderTexture smokeTex     = GetSmokeAlbedoTex();
        RenderTexture smokeMaskTex = GetSmokeMaskTex();

        // Depth pre-pass
        Graphics.Blit(source, depthTex, compositeMaterial, 0);

        // ── Bullet hole bitmask rebuild ────────────────────────────────────────
        // Lazy-initialize the mask texture once the voxel grid is available
        if (bulletHoleMaskTex == null) InitBulletHoleMask();

        int holeCount = gun != null ? gun.GetActiveBulletHoleCount() : 0;
        bool needsMaskRebuild = holeCount > 0 || wasBuildingBulletHoles;

        if (needsMaskRebuild) {
            SetSmokeVolumeUniforms();
            raymarchCompute.SetInt("_BulletHoleCount", holeCount);
            if (gun != null) {
                raymarchCompute.SetBuffer(bulletHoleBuildPass, "_BulletHoles", gun.GetBulletHoles());
                raymarchCompute.SetFloat("_BulletDepth", gun.GetDepth());
            }
            raymarchCompute.SetTexture(bulletHoleBuildPass, "_BulletHoleMaskTexRW", bulletHoleMaskTex);
            DispatchFullSmoke(bulletHoleBuildPass);
            wasBuildingBulletHoles = holeCount > 0;
        }

        // ── Camera matrices ────────────────────────────────────────────────────
        Matrix4x4 projMatrix     = GL.GetGPUProjectionMatrix(cam.projectionMatrix, false);
        Matrix4x4 viewProjMatrix = projMatrix * cam.worldToCameraMatrix;

        // Tell the compute shader whether to flip UV.y on this platform.
        // On D3D / Metal (graphicsUVStartsAtTop == true) render textures store rows
        // with Y=0 at the top, so the thread-ID-derived UV must be flipped to keep
        // the smoke rows aligned with the composite shader's blit UVs.
        raymarchCompute.SetInt("_FlipY", SystemInfo.graphicsUVStartsAtTop ? 1 : 0);

        raymarchCompute.SetVector("_CameraWorldPos",        transform.position);
        raymarchCompute.SetMatrix("_CameraToWorld",         cam.cameraToWorldMatrix);
        raymarchCompute.SetMatrix("_CameraInvProjection",   projMatrix.inverse);
        raymarchCompute.SetMatrix("_CameraInvViewProjection", viewProjMatrix.inverse);
        raymarchCompute.SetInt("_BufferWidth",  smokeTex.width);
        raymarchCompute.SetInt("_BufferHeight", smokeTex.height);

        // ── Smoke uniforms ─────────────────────────────────────────────────────
        raymarchCompute.SetInt("_StepCount",               stepCount);
        raymarchCompute.SetInt("_LightStepCount",          lightStepCount);
        raymarchCompute.SetFloat("_SmokeSize",             smokeSize);
        raymarchCompute.SetFloat("_FrameTime",             Time.time);
        raymarchCompute.SetFloat("_AbsorptionCoefficient", absorptionCoefficient);
        raymarchCompute.SetFloat("_ScatteringCoefficient", scatteringCoefficient);
        raymarchCompute.SetFloat("_DensityFalloff",        1f - densityFalloff);
        raymarchCompute.SetFloat("_VolumeDensity",         volumeDensity * stepSize);
        raymarchCompute.SetFloat("_StepSize",              stepSize);
        raymarchCompute.SetFloat("_ShadowDensity",         shadowDensity * lightStepSize);
        raymarchCompute.SetFloat("_LightStepSize",         lightStepSize);
        raymarchCompute.SetFloat("_G",                     scatteringAnisotropy);
        raymarchCompute.SetFloat("_AlphaThreshold",        alphaThreshold);
        raymarchCompute.SetVector("_SunDirection",         sun != null ? sun.transform.forward : Vector3.down);
        raymarchCompute.SetVector("_AnimationDirection",   animationDirection);
        raymarchCompute.SetInt("_PhaseFunction",           (int)phaseFunction);
        raymarchCompute.SetVector("_CubeParams",           cubeParams);
        raymarchCompute.SetVector("_LightColor",           (Vector4)lightColor);
        raymarchCompute.SetVector("_SmokeColor",           (Vector4)smokeColor);
        raymarchCompute.SetVector("_ExtinctionColor",      (Vector4)extinctionColor);
        raymarchCompute.SetVector("_Radius",               smokeVoxelData.GetCurrentRadius());
        raymarchCompute.SetVector("_SmokeOrigin",          smokeVoxelData.GetSmokeOrigin());

        if (debugNoise) {
            raymarchCompute.SetTexture(debugNoisePass, "_NoiseTex",  noiseTex);
            raymarchCompute.SetTexture(debugNoisePass, "_SmokeTex",  smokeTex);
            raymarchCompute.SetInt("_DebugNoiseSlice",  debugNoiseSlice);
            raymarchCompute.SetInt("_DebugAxis",        (int)debugNoiseAxis);
            raymarchCompute.SetInt("_DebugTiledNoise",  debugTiledNoise ? 1 : 0);
            raymarchCompute.SetVector("_NoiseRes",      new Vector4(128, 128, 128, 0));
            raymarchCompute.Dispatch(debugNoisePass,
                Mathf.CeilToInt(Screen.width / 8.0f),
                Mathf.CeilToInt(Screen.height / 8.0f), 1);
            Graphics.Blit(smokeTex, destination);
            return;
        }

        // ── Raymarching dispatch ───────────────────────────────────────────────
        raymarchCompute.SetTexture(raymarchSmokePass, "_SmokeTex",          smokeTex);
        raymarchCompute.SetTexture(raymarchSmokePass, "_SmokeMaskTex",      smokeMaskTex);
        raymarchCompute.SetTexture(raymarchSmokePass, "_NoiseTex",          noiseTex);
        raymarchCompute.SetTexture(raymarchSmokePass, "_DepthTex",          depthTex);
        raymarchCompute.SetTexture(raymarchSmokePass, "_SmokeVoxelTex",     smokeVoxelData.GetSmokeVoxelTexture());
        raymarchCompute.SetTexture(raymarchSmokePass, "_BulletHoleMaskTex", bulletHoleMaskTex);
        raymarchCompute.Dispatch(raymarchSmokePass,
            Mathf.CeilToInt(smokeTex.width  / 8.0f),
            Mathf.CeilToInt(smokeTex.height / 8.0f), 1);

        // ── Upscale ────────────────────────────────────────────────────────────
        if (resolutionScale == Res.HalfResolution) {
            Graphics.Blit(smokeMaskHalfTex, smokeMaskFullTex);
            Graphics.Blit(smokeMaskFullTex, smokeMaskHalfTex);
            Graphics.Blit(smokeAlbedoHalfTex, smokeAlbedoFullTex,
                bicubicUpscale ? compositeMaterial : null, bicubicUpscale ? 1 : -1);
        }

        if (resolutionScale == Res.QuarterResolution) {
            Graphics.Blit(smokeMaskQuarterTex, smokeMaskHalfTex);
            Graphics.Blit(smokeMaskHalfTex,    smokeMaskFullTex);
            Graphics.Blit(smokeMaskFullTex,    smokeMaskHalfTex);
            Graphics.Blit(smokeMaskHalfTex,    smokeMaskQuarterTex);

            if (bicubicUpscale) {
                Graphics.Blit(smokeAlbedoQuarterTex, smokeAlbedoHalfTex,  compositeMaterial, 1);
                Graphics.Blit(smokeAlbedoHalfTex,    smokeAlbedoFullTex,  compositeMaterial, 1);
            } else {
                Graphics.Blit(smokeAlbedoQuarterTex, smokeAlbedoHalfTex);
                Graphics.Blit(smokeAlbedoHalfTex,    smokeAlbedoFullTex);
            }
        }

        // ── Composite ──────────────────────────────────────────────────────────
        compositeMaterial.SetTexture("_SmokeTex",     smokeAlbedoFullTex);
        compositeMaterial.SetTexture("_SmokeMaskTex", smokeMaskTex);
        compositeMaterial.SetTexture("_DepthTex",     depthTex);
        compositeMaterial.SetFloat("_Sharpness",      sharpness);
        compositeMaterial.SetFloat("_DebugView",      (int)debugView);
        Graphics.Blit(source, destination, compositeMaterial, 2);
    }
}

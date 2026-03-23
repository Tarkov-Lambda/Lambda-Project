using System;
using UnityEngine;
using UnityEngine.Rendering;
using static System.Runtime.InteropServices.Marshal;

public class Voxelizer : MonoBehaviour {

    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Volume")]
    public Vector3 boundsExtent    = new Vector3(10, 5, 10);
    public float   voxelSize       = 0.25f;

    [Header("Fill")]
    public int     maxFillSteps = 16;
    public float   growthSpeed  = 0.4f;
    public bool    constantFill = false;
    /// <summary>
    /// The radius the smoke cloud grows to. Should be a rough sphere (equal XYZ) for
    /// a realistic grenade. Must be smaller than boundsExtent on every axis.
    /// </summary>
    public Vector3 maxRadius    = new Vector3(5f, 5f, 5f);

    [Header("Mesh Voxelization")]
    public GameObject[] objectsToVoxelize;
    public float        intersectionBias = 1.05f;

    [Header("Bake")]
    public SmokeVoxelBakeData bakeData;

    // ── Kernel indices ────────────────────────────────────────────────────────

    private const int KERNEL_CLEAR           = 0;
    private const int KERNEL_VOXELIZE_MESH   = 1;
    private const int KERNEL_SEED            = 2;
    private const int KERNEL_FILL            = 3;  // frontier-based, indirect
    private const int KERNEL_CLEAR_SMOKE_TEX = 4;
    private const int KERNEL_SEED_RADIUS_RING = 5;
    private const int KERNEL_BUILD_DISPATCH  = 6;

    // ── GPU resources ─────────────────────────────────────────────────────────

    public ComputeShader voxelizeCompute;

    // Persistent structured buffers
    private ComputeBuffer smokeVoxelsBuffer;   // RWStructuredBuffer<int> — fill source-of-truth
    private ComputeBuffer staticVoxelsBuffer;  // RWStructuredBuffer<int> — static geometry mask

    // Smoke voxel texture — written by fill kernels, read by raymarcher
    private RenderTexture smokeVoxelTex;

    // Frontier double-buffer
    private ComputeBuffer[] frontierBuffers      = new ComputeBuffer[2];
    private ComputeBuffer[] frontierCountBuffers = new ComputeBuffer[2];
    private int             frontierReadIdx      = 0;
    private ComputeBuffer   fillDispatchArgs;    // IndirectArguments buffer [x, y, z]

    // Accessors for the double-buffer swap pattern
    private ComputeBuffer FrontierRead       => frontierBuffers[frontierReadIdx];
    private ComputeBuffer FrontierWrite      => frontierBuffers[1 - frontierReadIdx];
    private ComputeBuffer FrontierReadCount  => frontierCountBuffers[frontierReadIdx];
    private ComputeBuffer FrontierWriteCount => frontierCountBuffers[1 - frontierReadIdx];

    // ── Runtime state ─────────────────────────────────────────────────────────

    private Vector3Int voxelResolution;
    private int        totalVoxels;
    private Vector3    smokeOrigin;

    private float  radius      = 0f;
    private bool   iterateFill = false;
    private bool   isSmokeSettled = false;
    private AsyncGPUReadbackRequest? settleRequest = null;

    // Radius vector from last frame — passed to CS_SeedRadiusRing
    private Vector3 prevRadius = Vector3.zero;

    // ── Public API ────────────────────────────────────────────────────────────

    public Vector3    GetBoundsExtent()     => boundsExtent;
    public Vector3    GetVoxelResolution()  => (Vector3)voxelResolution;
    public float      GetVoxelSize()        => voxelSize;
    public Vector3    GetSmokeOrigin()      => smokeOrigin;
    /// <summary>Returns the current interpolated smoke radius (grows toward maxRadius, not boundsExtent).</summary>
    public Vector3    GetCurrentRadius()    => Vector3.Lerp(Vector3.zero, maxRadius, Easing(radius));
    public RenderTexture GetSmokeVoxelTexture() => smokeVoxelTex;

    /// <summary>Returns the raw smoke voxel buffer (still needed by VoxelizerBaker).</summary>
    public ComputeBuffer GetSmokeVoxelBuffer() => smokeVoxelsBuffer;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void OnEnable() {
// #if UNITY_EDITOR
//         voxelizeCompute = Resources.Load<ComputeShader>("Voxelize");
// #endif

        // Compute grid dimensions
        Vector3 boundsSize = boundsExtent * 2;
        int rx = Mathf.CeilToInt(boundsSize.x / voxelSize);
        int ry = Mathf.CeilToInt(boundsSize.y / voxelSize);
        int rz = Mathf.CeilToInt(boundsSize.z / voxelSize);
        voxelResolution = new Vector3Int(rx, ry, rz);
        totalVoxels = rx * ry * rz;

        // Structured buffers
        smokeVoxelsBuffer  = new ComputeBuffer(totalVoxels, sizeof(int));
        staticVoxelsBuffer = new ComputeBuffer(totalVoxels, sizeof(int));

        // Smoke voxel texture (RFloat 3D, hardware-trilinear)
        smokeVoxelTex = new RenderTexture(rx, ry, 0, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear) {
            enableRandomWrite = true,
            dimension         = UnityEngine.Rendering.TextureDimension.Tex3D,
            volumeDepth       = rz,
            filterMode        = FilterMode.Bilinear,
            wrapMode          = TextureWrapMode.Clamp
        };
        smokeVoxelTex.Create();

        // Frontier buffers — each large enough to hold all voxels in the worst case
        for (int i = 0; i < 2; ++i) {
            frontierBuffers[i]      = new ComputeBuffer(totalVoxels, sizeof(uint));
            frontierCountBuffers[i] = new ComputeBuffer(1, sizeof(uint));
        }
        frontierCountBuffers[0].SetData(new uint[] { 0 });
        frontierCountBuffers[1].SetData(new uint[] { 0 });

        // Indirect dispatch args buffer
        fillDispatchArgs = new ComputeBuffer(3, sizeof(uint), ComputeBufferType.IndirectArguments);

        // Set static uniforms shared by all kernels
        SetSharedUniforms();

        // Static geometry voxelization (or restore from bake data)
        ClearBuffer(staticVoxelsBuffer);
        if (!TryLoadStaticBake()) {
            RunMeshVoxelization();
        }

        // Clear smoke state
        ClearBuffer(smokeVoxelsBuffer);
        voxelizeCompute.SetTexture(KERNEL_CLEAR_SMOKE_TEX, "_SmokeVoxelTex", smokeVoxelTex);
        DispatchFull(KERNEL_CLEAR_SMOKE_TEX);
    }

    void OnDisable() {
        smokeVoxelsBuffer?.Release();
        staticVoxelsBuffer?.Release();
        smokeVoxelTex?.Release();
        for (int i = 0; i < 2; ++i) {
            frontierBuffers[i]?.Release();
            frontierCountBuffers[i]?.Release();
        }
        fillDispatchArgs?.Release();
    }

    void Update() {
        // iterateFill stays true from smoke throw until isSmokeSettled becomes true.
        // constantFill keeps the fill running independently of throw state.
        bool shouldFill = (iterateFill || constantFill) && !isSmokeSettled;
        if (!shouldFill) return;

        CheckSettleReadback();
        RunFillStep();
    }

    // ── Smoke throw ───────────────────────────────────────────────────────────

    public void HandleSmokeThrow(Vector3 origin) {
        smokeOrigin = origin;
        radius      = 0f;
        prevRadius  = Vector3.zero;
        isSmokeSettled = false;
        settleRequest  = null;
        frontierReadIdx = 0;

        // Clear smoke structured buffer
        ClearBuffer(smokeVoxelsBuffer);

        // Clear smoke texture
        voxelizeCompute.SetVector("_BoundsExtent",   boundsExtent);
        voxelizeCompute.SetVector("_VoxelResolution", (Vector3)voxelResolution);
        voxelizeCompute.SetTexture(KERNEL_CLEAR_SMOKE_TEX, "_SmokeVoxelTex", smokeVoxelTex);
        DispatchFull(KERNEL_CLEAR_SMOKE_TEX);

        // Reset frontier counts
        frontierCountBuffers[0].SetData(new uint[] { 0 });
        frontierCountBuffers[1].SetData(new uint[] { 0 });

        // Seed smoke + initialize write frontier
        voxelizeCompute.SetVector("_SmokeOrigin", smokeOrigin);
        voxelizeCompute.SetBuffer(KERNEL_SEED, "_SmokeVoxels",       smokeVoxelsBuffer);
        voxelizeCompute.SetBuffer(KERNEL_SEED, "_FrontierWrite",      FrontierWrite);
        voxelizeCompute.SetBuffer(KERNEL_SEED, "_FrontierWriteCount", FrontierWriteCount);
        voxelizeCompute.SetTexture(KERNEL_SEED, "_SmokeVoxelTex",    smokeVoxelTex);
        voxelizeCompute.Dispatch(KERNEL_SEED, 1, 1, 1);

        // Swap: CS_Seed wrote to FrontierWrite/WriteCount → make it the read frontier
        SwapFrontierBuffers();

        iterateFill = true;
    }

    // ── Fill step (frontier-based, indirect dispatch) ─────────────────────────

    void RunFillStep() {
        // Compute radius for this frame — grows toward maxRadius, NOT boundsExtent.
        // boundsExtent is the grid boundary; maxRadius is the smoke sphere size.
        Vector3 prevRadiusVec    = Vector3.Lerp(Vector3.zero, maxRadius, Easing(radius));
        radius                  += growthSpeed * Time.deltaTime;
        Vector3 currentRadiusVec = Vector3.Lerp(Vector3.zero, maxRadius, Easing(radius));

        SetSharedUniforms();
        voxelizeCompute.SetVector("_PrevRadius",    prevRadiusVec);
        voxelizeCompute.SetVector("_Radius",        currentRadiusVec);
        voxelizeCompute.SetVector("_SmokeOrigin",   smokeOrigin);
        voxelizeCompute.SetInt("_MaxFillSteps",     maxFillSteps);
        voxelizeCompute.SetInt("_FrontierCapacity", totalVoxels);

        // 1. Build indirect dispatch args from read count; reset write count
        voxelizeCompute.SetBuffer(KERNEL_BUILD_DISPATCH, "_FrontierReadCount",  FrontierReadCount);
        voxelizeCompute.SetBuffer(KERNEL_BUILD_DISPATCH, "_FrontierWriteCount", FrontierWriteCount);
        voxelizeCompute.SetBuffer(KERNEL_BUILD_DISPATCH, "_DispatchArgs",       fillDispatchArgs);
        voxelizeCompute.Dispatch(KERNEL_BUILD_DISPATCH, 1, 1, 1);

        // 2. Frontier fill (indirect — zero groups when frontier is empty)
        voxelizeCompute.SetBuffer(KERNEL_FILL, "_FrontierRead",       FrontierRead);
        voxelizeCompute.SetBuffer(KERNEL_FILL, "_FrontierWrite",      FrontierWrite);
        voxelizeCompute.SetBuffer(KERNEL_FILL, "_FrontierReadCount",  FrontierReadCount);
        voxelizeCompute.SetBuffer(KERNEL_FILL, "_FrontierWriteCount", FrontierWriteCount);
        voxelizeCompute.SetBuffer(KERNEL_FILL, "_SmokeVoxels",        smokeVoxelsBuffer);
        voxelizeCompute.SetBuffer(KERNEL_FILL, "_StaticVoxels",       staticVoxelsBuffer);
        voxelizeCompute.SetTexture(KERNEL_FILL, "_SmokeVoxelTex",     smokeVoxelTex);
        voxelizeCompute.DispatchIndirect(KERNEL_FILL, fillDispatchArgs);

        // 3. Seed radius ring (adds voxels newly within the radius to write frontier)
        voxelizeCompute.SetBuffer(KERNEL_SEED_RADIUS_RING, "_FrontierWrite",      FrontierWrite);
        voxelizeCompute.SetBuffer(KERNEL_SEED_RADIUS_RING, "_FrontierWriteCount", FrontierWriteCount);
        voxelizeCompute.SetBuffer(KERNEL_SEED_RADIUS_RING, "_SmokeVoxels",        smokeVoxelsBuffer);
        DispatchFull(KERNEL_SEED_RADIUS_RING);

        // Request non-blocking readback of write count to detect settle
        if (!settleRequest.HasValue) {
            settleRequest = AsyncGPUReadback.Request(FrontierWriteCount);
        }

        // 4. Swap double buffers: write frontier becomes next frame's read frontier
        SwapFrontierBuffers();

        prevRadius = currentRadiusVec;
    }

    // ── Settle detection ──────────────────────────────────────────────────────

    void CheckSettleReadback() {
        if (!settleRequest.HasValue) return;
        var req = settleRequest.Value;
        if (!req.done) return;

        settleRequest = null;
        if (req.hasError) return;

        var data = req.GetData<uint>();
        if (data.Length > 0 && data[0] == 0 && Easing(radius) >= 1.0f) {
            isSmokeSettled = true;
            Debug.Log("[Voxelizer] Smoke settled — fill loop stopped.");
        }
    }

    float Easing(float x) {
        if (x < 0.5f) return 4f * x * x * x;
        float f = -2f * x + 2f;
        return 1f - f * f * f / 2f;
    }

    // ── Buffer utilities ──────────────────────────────────────────────────────

    void ClearBuffer(ComputeBuffer buffer) {
        voxelizeCompute.SetBuffer(KERNEL_CLEAR, "_Voxels", buffer);
        DispatchFull(KERNEL_CLEAR);
    }

    void DispatchFull(int kernel) {
        int gx = Mathf.CeilToInt(voxelResolution.x / 4.0f);
        int gy = Mathf.CeilToInt(voxelResolution.y / 4.0f);
        int gz = Mathf.CeilToInt(voxelResolution.z / 8.0f);
        voxelizeCompute.Dispatch(kernel, gx, gy, gz);
    }

    void SwapFrontierBuffers() {
        frontierReadIdx = 1 - frontierReadIdx;
    }

    void SetSharedUniforms() {
        voxelizeCompute.SetVector("_BoundsExtent",    boundsExtent);
        voxelizeCompute.SetVector("_VoxelResolution", (Vector3)voxelResolution);
        voxelizeCompute.SetFloat("_VoxelSize",        voxelSize);
        voxelizeCompute.SetFloat("_IntersectionBias", intersectionBias);
        voxelizeCompute.SetInt("_MaxFillSteps",       maxFillSteps);
        voxelizeCompute.SetInt("_FrontierCapacity",   totalVoxels);
    }

    // ── Static mesh voxelization ──────────────────────────────────────────────

    bool TryLoadStaticBake() {
        if (bakeData == null || bakeData.staticVoxelData == null) return false;

        Vector3Int res = voxelResolution;
        if (!bakeData.IsCompatible(res, boundsExtent, voxelSize)) {
            Debug.LogWarning("[Voxelizer] Bake data is stale — falling back to live voxelization.");
            return false;
        }

        int[] intData = new int[bakeData.staticVoxelData.Length];
        for (int i = 0; i < intData.Length; ++i) intData[i] = bakeData.staticVoxelData[i];
        staticVoxelsBuffer.SetData(intData);
        return true;
    }

    void RunMeshVoxelization() {
#if UNITY_EDITOR
        if (objectsToVoxelize == null || objectsToVoxelize.Length == 0) return;

        voxelizeCompute.SetBuffer(KERNEL_VOXELIZE_MESH, "_StaticVoxels", staticVoxelsBuffer);

        foreach (var go in objectsToVoxelize) {
            if (go == null) continue;
            var meshFilter = go.GetComponentInChildren<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null) continue;

            Mesh mesh = meshFilter.sharedMesh;
            int[] triangles = mesh.triangles;
            Vector3[] vertices = mesh.vertices;

            using var vertexBuf   = new ComputeBuffer(vertices.Length, SizeOf(typeof(Vector3)));
            using var triangleBuf = new ComputeBuffer(triangles.Length, sizeof(int));
            vertexBuf.SetData(vertices);
            triangleBuf.SetData(triangles);

            voxelizeCompute.SetBuffer(KERNEL_VOXELIZE_MESH, "_MeshVertices",       vertexBuf);
            voxelizeCompute.SetBuffer(KERNEL_VOXELIZE_MESH, "_MeshTriangleIndices", triangleBuf);
            voxelizeCompute.SetMatrix("_MeshLocalToWorld", go.transform.localToWorldMatrix);
            voxelizeCompute.SetInt("_TriangleCount", triangles.Length);
            DispatchFull(KERNEL_VOXELIZE_MESH);
        }
#else
        Debug.LogWarning("[Voxelizer] No bake data assigned — static voxels unavailable in builds.");
#endif
    }

    // ── Editor bake API (called by VoxelizerBaker) ────────────────────────────

#if UNITY_EDITOR
    public void BakeStaticVoxels() {
        if (objectsToVoxelize == null || objectsToVoxelize.Length == 0) {
            Debug.LogWarning("[Voxelizer] Nothing to bake — assign objectsToVoxelize.");
            return;
        }

        OnEnable(); // ensure buffers are fresh

        // Read back static voxel data
        int[] data = new int[totalVoxels];
        staticVoxelsBuffer.GetData(data);

        byte[] byteData = new byte[totalVoxels];
        for (int i = 0; i < totalVoxels; ++i) byteData[i] = (byte)Mathf.Clamp(data[i], 0, 1);

        if (bakeData == null) {
            bakeData = UnityEditor.AssetDatabase.LoadAssetAtPath<SmokeVoxelBakeData>(
                "Assets/SmokeVoxelBakeData.asset");
            if (bakeData == null) {
                bakeData = ScriptableObject.CreateInstance<SmokeVoxelBakeData>();
                UnityEditor.AssetDatabase.CreateAsset(bakeData, "Assets/SmokeVoxelBakeData.asset");
            }
        }

        bakeData.resolution      = voxelResolution;
        bakeData.boundsExtent    = boundsExtent;
        bakeData.voxelSize       = voxelSize;
        bakeData.staticVoxelData = byteData;

        UnityEditor.EditorUtility.SetDirty(bakeData);
        UnityEditor.AssetDatabase.SaveAssets();
        Debug.Log($"[Voxelizer] Baked {totalVoxels:N0} voxels into {bakeData.name}.");

        OnDisable();
    }
#endif

    // ── Gizmo ─────────────────────────────────────────────────────────────────

    void OnDrawGizmos() {
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.3f);
        Gizmos.DrawWireCube(new Vector3(0, boundsExtent.y, 0), boundsExtent * 2);
    }
}
